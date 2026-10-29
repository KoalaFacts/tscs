import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual checked ES2015 JavaScript execution, not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-try-finally-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item = {value:number};\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export let visits:number=0;',
    'export function compute(n:number):number {',
    '  const item:Item={value:n};',
    '  try { if(n<0){throw item.value;} return item.value+1; }',
    '  finally { visits+=1; }',
    '}',
    'export function override(n:number):number {',
    '  try {if(n<0){throw n;} return n;} finally {return 42;}',
    '}',
    'export function cleanupThrow(n:number):number {',
    '  try {return n;} finally {throw "cleanup";}',
    '}',
    'export function loop(n:number):number {',
    '  let counter:number=0;',
    '  while(n>0){try {n--;if(n===2){continue;}} finally {counter+=1;}}',
    '  return counter+39;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),
    'export {compute,override,cleanupThrow,loop,visits} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {compute,override,loop} from "./bridge.js";',
    'export const result:number=compute(41);',
    'export const overridden:number=override(-1);',
    'export const loopResult:number=loop(3);'
  ].join('\n'));
  function compile(source:string) {
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing generated '+name);
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,overridden,loopResult} from "./entry.js";',
    'import {compute,override,cleanupThrow,visits} from "./bridge.js";',
    'if(result!==42||overridden!==42||loopResult!==42)throw Error("try/finally runtime mismatch");',
    'let errors=0;',
    'try{compute(-7);}catch(e){if(e!==-7)throw e;errors++;}',
    'try{cleanupThrow(21);}catch(e){if(e!=="cleanup")throw e;errors++;}',
    'if(override(7)!==42||errors!==2||visits!==2)throw Error("cleanup override or effect mismatch");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid=[
    ['missing-finally','function f():number {try {return 42;}}','TSCS1001'],
    ['destructured-catch-unsupported','function f():number {try {return 42;}catch({code}){return 21;}}','TSCS1001'],
    ['wrong-return','function f():number {try {return 42;}finally{return "bad";}}','TS2322'],
    ['unresolved-cleanup','function f():number {try{return 42;}finally{throw missing;}}','TS2304'],
    ['wrong-try','function f():number {try{return "bad";}finally{let x:number=1;}}','TS2322']
  ] as const;
  for(const [name,source,code] of invalid) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(name+'.ts');
    assert.notEqual(result.status,0,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JavaScript');
  }
  console.log('Try/finally: four-module runtime result=42, 2 cleanup exceptions, loop=42, 5 no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
