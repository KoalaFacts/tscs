import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Executed four-module ES2015 output, not full original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-template-literal-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/string-primitives/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export type Caption = { text: string };',
    'export const base: string = `alpha`;'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Caption } from "./model.js";',
    'export function decorate(c: Caption): string { return c.text + `!`; }',
    'export function newline(): string { return `one\\ntwo`; }',
    'export function escaped(): string { return `tick: \\` and \\${name}`; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { decorate, newline, escaped } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { base } from "./model.js";',
    'import { decorate, newline, escaped } from "./bridge.js";',
    'export const result: string = decorate({ text: base });',
    'export const lines: string = newline();',
    'export const quoted: string = escaped();',
    'export const literal: "fixed" = `fixed`;'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compiled=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','-p',join(folder,'project.json')],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(compiled.error,undefined,String(compiled.error));
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,lines,quoted,literal} from "./entry.js"; if(result!=="alpha!"||lines!=="one\\ntwo"||quoted!=="tick: ` and ${name}"||literal!=="fixed") throw Error("template runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid=[
    ['number-mismatch','let n: number = `hello`;','TS2322',2],
    ['wrong-literal','let s: "target" = `other`;','TS2322',2],
    ['unknown-member','let s: string = `hello`.missing;','TS2339',2],
    ['unimplemented-interpolation','let s: string = `hello${21}`;','TSCS1001',1],
  ] as const;
  for(const [name,source,code,exit] of invalid){
    const path=join(folder,name+'.ts');
    writeFileSync(path,source);
    const rejected=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015',path,join(folder,'minimal.d.ts')],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(rejected.error,undefined,String(rejected.error));
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,name+' emitted JS');
  }
  console.log('no-substitution templates: 4 linked ES2015 modules executed; 4 invalid programs blocked.');
} finally {rmSync(folder,{recursive:true,force:true});}
