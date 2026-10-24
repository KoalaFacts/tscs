import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,readFileSync,symlinkSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {generateInventory} from './inventory-ts7.mjs';

const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const readLines=p=>readFileSync(p,'utf8').trim().split('\n').filter(Boolean).map(x=>JSON.parse(x));
function repository(t) {
  const root=mkdtempSync(join(tmpdir(),'ts7-inventory-'));
  t.after(()=>rmSync(root,{recursive:true,force:true}));
  const upstream=join(root,'upstream');mkdirSync(upstream);
  const git=(...args)=>execFileSync('git',['-C',upstream,...args],{encoding:'utf8'}).trim();
  git('init','-q');git('config','user.name','Inventory test');git('config','user.email','inventory@example.invalid');
  const files={
    'tsc/testdata/tests/cases/compiler/same.ts':'// @target: es5, esnext\n// @Filename: ../virtual.ts\n',
    'tsc/testdata/tests/cases/conformance/same.ts':'// @strict: true\n',
    'tsc/testdata/tests/cases/compiler/skip.ts':'// @strict: true\n',
    'tsc/internal/unit/a_test.go':'package unit\nimport "testing"\nfunc TestOne(t *testing.T) { for _, n := range []string{"x","y"} { t.Run(n, func(t *testing.T){ t.Skip("condition") }) } }\n',
    'tsc/internal/testrunner/compiler_runner.go':'package testrunner\nvar skippedTests = []string{"skip.ts"}\n',
    'packages/typescript/test/api.test.ts':'import { test } from "node:test"; test("one",()=>{});\n',
    'new/surprise.xyz':'unclassified but retained\n',
  };
  for(const [path,source] of Object.entries(files)){mkdirSync(join(upstream,path,'..'),{recursive:true});writeFileSync(join(upstream,path),source);}
  writeFileSync(join(upstream,'tsc/testdata/tests/cases/compiler/invalid.ts'),Buffer.from([0xff,0x00]));
  symlinkSync('/never/follow/this',join(upstream,'escape.ts'));
  git('add','.');git('commit','-qm','test source');const first=git('rev-parse','HEAD');
  git('update-index','--add','--cacheinfo',`160000,${first},vendor/typescript`);git('commit','-qm','gitlink scope');
  return {root,upstream,pin:git('rev-parse','HEAD'),git};
}

test('generation reconciles committed bytes, skips, dynamic declarations and unexpanded gitlinks',async t=>{
  const ctx=repository(t), output=join(ctx.root,'result');
  writeFileSync(join(ctx.upstream,'new/surprise.xyz'),'dirty working copy must not affect inventory');
  writeFileSync(join(ctx.upstream,'untracked_test.go'),'not in pinned tree');
  const result=await generateInventory({upstream:ctx.upstream,pin:ctx.pin,output});
  const assets=readLines(join(output,'assets.jsonl')), fixtures=readLines(join(output,'fixtures.jsonl')), declarations=readLines(join(output,'declarations.jsonl'));
  assert.equal(assets.length,10);
  assert.equal(assets.find(x=>x.path==='new/surprise.xyz').sha256,hash(Buffer.from('unclassified but retained\n')));
  assert.equal(assets.some(x=>x.path==='untracked_test.go'),false);
  assert.equal(assets.find(x=>x.path==='escape.ts').role,'git-symlink');
  assert.equal(assets.find(x=>x.path==='escape.ts').linkTargetUtf8,'/never/follow/this');
  assert.equal(result.provenance.gitlinks[0].path,'vendor/typescript');
  assert.equal(result.provenance.gitlinks[0].expanded,false);
  assert.equal(fixtures.length,4);
  assert.equal(new Set(fixtures.map(x=>x.id)).size,4);
  assert.equal(fixtures.find(x=>x.path.endsWith('/skip.ts')).upstreamSelection.status,'UPSTREAM_SKIPPED');
  const declaration=declarations.find(x=>x.name==='TestOne');
  assert.equal(declaration.runCalls[0].literalName,null);
  assert.equal(declaration.skipCalls.length,1);
  assert.equal(declaration.expansion.status,'UNEXPANDED');
  assert.equal(declaration.progress.acceptance,'NOT_RUN');
  assert.equal(result.counts.concreteCases,null);
  assert.equal(result.counts.releaseEligible,false);
});

test('repeated generation yields identical artifacts and verifiable checksums',async t=>{
  const ctx=repository(t), first=join(ctx.root,'first'), second=join(ctx.root,'second');
  const a=await generateInventory({upstream:ctx.upstream,pin:ctx.pin,output:first});
  const b=await generateInventory({upstream:ctx.upstream,pin:ctx.pin,output:second});
  assert.deepEqual(a,b);
  for(const item of a.artifacts) {
    const bytes=readFileSync(join(first,item.path));
    assert.deepEqual(bytes,readFileSync(join(second,item.path)));
    assert.equal(bytes.length,item.bytes);
    assert.equal(hash(bytes),item.sha256);
  }
  assert.deepEqual(readFileSync(join(first,'summary.json')),readFileSync(join(second,'summary.json')));
});

test('wrong pins and preexisting output are rejected without overwriting',async t=>{
  const ctx=repository(t), output=join(ctx.root,'existing');mkdirSync(output);writeFileSync(join(output,'precious'),'keep');
  await assert.rejects(generateInventory({upstream:ctx.upstream,pin:'0'.repeat(40),output:join(ctx.root,'wrong')}),/pin|commit|object/i);
  await assert.rejects(generateInventory({upstream:ctx.upstream,pin:ctx.pin,output}),/exist|empty/i);
  assert.equal(readFileSync(join(output,'precious'),'utf8'),'keep');
});

test('partial or unparsable skip metadata remains unresolved, not an asserted upstream skip',async t=>{
  const ctx=repository(t);
  for(const [label,source] of [
    ['dynamic','package testrunner\nvar skippedTests = []string{"skip.ts", generatedName}\n'],
    ['broken','package testrunner\nvar skippedTests = []string{"skip.ts"}\nfunc broken(\n'],
  ]) {
    writeFileSync(join(ctx.upstream,'tsc/internal/testrunner/compiler_runner.go'),source);
    ctx.git('add','.');ctx.git('commit','-qm',label);const pin=ctx.git('rev-parse','HEAD');
    const output=join(ctx.root,label);await generateInventory({upstream:ctx.upstream,pin,output});
    const fixtures=readLines(join(output,'fixtures.jsonl'));
    assert.equal(fixtures.find(x=>x.path.endsWith('/skip.ts')).upstreamSelection.status,'SELECTION_POLICY_UNRESOLVED');
  }
});

test('Go syntax candidates explicitly retain unevaluated selection constraints',async t=>{
  const ctx=repository(t), output=join(ctx.root,'constraints');
  await generateInventory({upstream:ctx.upstream,pin:ctx.pin,output});
  const declaration=readLines(join(output,'declarations.jsonl')).find(x=>x.name==='TestOne');
  assert.deepEqual(declaration.selection,{kind:'STATIC_GO_DECLARATION',runtimeDiscovered:false,buildConstraintsEvaluated:false,filename:'a_test.go'});
});

test('toolchain provenance requires pinned Go and is recorded in the result',async t=>{
  const ctx=repository(t), output=join(ctx.root,'toolchain');
  const result=await generateInventory({upstream:ctx.upstream,pin:ctx.pin,output});
  assert.deepEqual(result.provenance.toolchain,{go:'go1.27.3',nodeMajor:24});
});
