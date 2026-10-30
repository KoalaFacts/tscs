import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const dir=mkdtempSync(join(tmpdir(),'tscs-function-expression-'));
try {
  writeFileSync(join(dir,'package.json'), '{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  const files={
    'model.ts':'export type Callback=(n:number)=>number;',
    'logic.ts':[
      'import type {Callback} from "./model.js";',
      'const offset:number=1;',
      'export const inc:Callback=function(n:number):number{return n+offset;};',
      'export function apply(cb:Callback,n:number):number{return cb(n);}'
    ].join('\n'),
    'bridge.ts':'export {inc,apply} from "./logic.js";',
    'entry.ts':[
      'import {inc,apply} from "./bridge.js";',
      'const doubled=function(n:number):number{return n+n;};',
      'export const result:number=apply(doubled,21);',
      'export const captured:number=inc(41);',
      'export const immediate:number=apply(function(n:number):number{return n+n;},21);',
      'const make=function(n:number):(m:number)=>number{return function(m:number):number{return n+m;};};',
      'const nestedCallback=make(20);',
      'export const nested:number=nestedCallback(22);',
      'const rested=function(base:number,...xs:number[]):number{let total:number=base;for(const item of xs){total+=item;}return total;};',
      'export const rest:number=rested(20,21,1);',
      'const inferred=function(n:number){return n+n;};',
      'export const inferredResult:number=inferred(21);',
      'const rec=function self(n:number):number{if(n<=0){return 42;}return self(n-1);};',
      'export const recursiveResult:number=rec(3);',
      'const external:number=21;',
      'const scoped=function external(n:number):number{return n+n;};',
      'export const scopedResult:number=scoped(external);',
      'const namedDefault=function self(n:number=21):number{return n+n;};',
      'export const namedDefaultResult:number=namedDefault();',
      'const namedRest=function add(base:number,...xs:number[]):number{let sum:number=base;for(const item of xs){sum+=item;}return sum;};',
      'export const namedRestResult:number=namedRest(20,21,1);',
      'const namedFactory=function outer(n:number):(v:number)=>number{return function inner(v:number):number{return n+v;};};',
      'const namedNested=namedFactory(20);',
      'export const namedNestedResult:number=namedNested(22)'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(files))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]){
    const result=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing checked output '+name);
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,captured,immediate,nested,rest,inferredResult,recursiveResult,scopedResult,namedDefaultResult,namedRestResult,namedNestedResult} from "./entry.js"; if([result,captured,immediate,nested,rest,inferredResult,recursiveResult,scopedResult,namedDefaultResult,namedRestResult,namedNestedResult].some(x=>x!==42))throw Error("function expression output mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['return','const f=function(n:number):number{return "bad";};','TS2322',2],
    ['argument','const f=function(n:number):number{return n;};f("bad");','TS2345',2],
    ['arity','const f=function(n:number):number{return n;};f();','TS2554',2],
    ['missing','const f=function(n:number):number{return absent;};','TS2304',2],
    ['partial','const f=function(n:number):number{if(n>0){return n;}};','TSCS1001',1],
    ['untyped','const f=function(n){return n;};','TSCS1001',1],
    ['named-inferred','const f=function named(n:number){return n;};','TSCS1001',1],
    ['named-leak','const f=function inner(n:number):number{return n;};inner(42);','TS2304',2],
    ['named-argument','const f=function self(n:number):number{return self("bad");};','TS2345',2],
    ['named-rebind','const f=function self(n:number):number{self=self;return n;};','TS2588',2],
    ['default-type','const f=function(n:number="bad"):number{return n;};','TS2322',2],
    ['rest-array','const f=function(...xs:number):number{return 42;};','TS2370',2]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'invalid-'+id+'.ts');
    writeFileSync(path,source);
    const checked=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(checked.status,exit,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid source emitted JS: '+id);
  }
  console.log('typed anonymous function expressions: four-module ES2015 runtime and twelve fail-closed no-emit programs checked');
} finally {
  rmSync(dir,{recursive:true,force:true});
}
