import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Checked four-module ES2015 execution; not original upstream acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-void-return-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item={value:number};\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export let total:number=0;',
    'export function note(n:number):void {',
    '  const item:Item={value:n};',
    '  if(n<0){return;} total+=item.value;',
    '}',
    'export function early(n:number):void {',
    '  try {if(n<0){return;} total+=n;} finally {total+=1;}',
    '}',
    'export function callThrough(n:number):void {note(n);early(n);return;}',
    'export function noReturn(n:number):void {total+=n;}',
    'export function delegate():void {return noReturn(0);}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),
    'export {callThrough,delegate,total} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {callThrough,delegate} from "./bridge.js";',
    'callThrough(20);callThrough(-3);delegate();',
    'export const result:number=42;'
  ].join('\n'));
  function compile(source:string){
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
    'import {result} from "./entry.js";',
    'import {total} from "./bridge.js";',
    'if(result!==42||total!==42)throw Error("incorrect void function side effects");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid=[
    ['wrong-return','function f():void {return 42;}','TS2322'],
    ['wrong-string-return','function f():void {return "bad";}','TS2322'],
    ['wrong-call-usage','function f():void {} let n:number=f();','TS2322'],
    ['bare-number-return','function f():number {return;}','TSCS1001'],
    ['missing-name','function f():void {return missing;}','TS2304'],
    ['missing-numeric-return','function f():number {let n:number=1;}','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(name+'.ts');
    assert.notEqual(result.status,0,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid source emitted JavaScript');
  }
  console.log('Void-return: four-module ES2015 result=42, total=42, early return/finally checked; six no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
