import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,cpSync,readFileSync,writeFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {spawnSync} from 'node:child_process';
const root=resolve('.');
function tamper(change){
 const dir=mkdtempSync(join(tmpdir(),'tscs-evidence-'));
 try{
  for(const path of ['fixtures','tests/conformance','upstream.lock.json','src/Tscs.Cli/bin/Release/net10.0','src/Tscs.Compiler/bin/Release/net10.0'])cpSync(join(root,path),join(dir,path),{recursive:true});
  change(dir);
  return spawnSync(process.execPath,[join(root,'tools/verify-conformance.mjs')],{cwd:dir,encoding:'utf8',timeout:10000});
 }finally{rmSync(dir,{recursive:true,force:true});}
}
test('altered expected diagnostics fail before running compiler',()=>{
 const r=tamper(d=>writeFileSync(join(d,'tests/conformance/expected.json'),'{}'));
 assert.notEqual(r.status,0);assert.match(r.stderr,/oracle evidence hash mismatch/);
});
test('altered fixture bytes fail before running compiler',()=>{
 const r=tamper(d=>writeFileSync(join(d,'fixtures/upstream/simpleTestSingleFile.ts'),'const x = 1;'));
 assert.notEqual(r.status,0);assert.match(r.stderr,/fixture hash mismatch/);
});
test('an empty manifest cannot produce a passing report',()=>{
 const r=tamper(d=>{const p=join(d,'tests/conformance/manifest.json');const m=JSON.parse(readFileSync(p));m.cases=[];writeFileSync(p,JSON.stringify(m));});
 assert.notEqual(r.status,0);assert.match(r.stderr,/empty selection/);
});
test('reduced cohort is rejected against frozen evidence',()=>{
 const r=tamper(d=>{const p=join(d,'tests/conformance/manifest.json');const m=JSON.parse(readFileSync(p));m.cases.pop();writeFileSync(p,JSON.stringify(m));});
 assert.notEqual(r.status,0);assert.match(r.stderr,/missing or additional oracle cases/);
});
test('altered corpus denominator cannot pass',()=>{
 const r=tamper(d=>writeFileSync(join(d,'tests/conformance/inventory-summary.json'),JSON.stringify({commit:'wrong',files:0,knownVariants:0,unresolvedVariantFiles:0,inventorySha256:'invalid'})));
 assert.notEqual(r.status,0);assert.match(r.stderr,/inventory evidence hash mismatch/);
});
