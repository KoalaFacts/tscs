import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Checked ES2015 runtime; not original TS7 harness acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-throw-flow-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Problem = { code: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Problem } from "./model.js";',
    'export function double(n:number):number { if(n<0){throw n;} return n*2; }',
    'export function next(n:number):number {',
    '  if(n<0){const error:Problem={code:n};throw error;}',
    '  return n+1;',
    '}',
    'export function selected(n:number):number {',
    '  switch(n){case 21:{return 42;}default:{throw "invalid";}}',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {double,next,selected} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {double,next,selected} from "./bridge.js";',
    'export const result:number=next(41);',
    'export const doubled:number=double(21);',
    'export const switched:number=selected(21);'
  ].join('\n'));
  function compile(source:string) {
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compilation timed out or crashed');
    return result;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing generated '+name);
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,doubled,switched} from "./entry.js";',
    'import {double,next,selected} from "./bridge.js";',
    'if(result!==42||doubled!==42||switched!==42)throw Error("return/throw value mismatch");',
    'let errors=0;',
    'try{next(-7);}catch(e){if(e.code!==-7)throw e;errors++;}',
    'try{double(-1);}catch(e){if(e!==-1)throw e;errors++;}',
    'try{selected(0);}catch(e){if(e!=="invalid")throw e;errors++;}',
    'if(errors!==3)throw Error("missing runtime throw paths");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid=[
    ['unresolved','function f():number { throw missing; }','TS2304'],
    ['empty','function f():number { throw; }','TS1109'],
    ['line-break','function f():number { throw\n 21; }','TS1142'],
    ['bad-assert','function f():number { throw (21 as string); }','TS2352'],
    ['missing-termination','function f(n:number):number { if(n>0){throw n;} }','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(name+'.ts');
    assert.notEqual(result.status,0,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JavaScript');
  }
  console.log('Throw flow: four-module ES2015 result=42 doubled=42 switched=42; three runtime exceptions, five no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
