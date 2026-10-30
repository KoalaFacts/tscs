import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const dir=mkdtempSync(join(tmpdir(),'tscs-typed-arrows-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const files={
    'model.ts':'export type Callback=(n:number)=>number;',
    'logic.ts':[
      'import type {Callback} from "./model.js";',
      'const extra:number=1;',
      'export const inc:Callback=(n:number)=>{return n+extra;};',
      'export function apply(fn:Callback,n:number):number{return fn(n);}',
      'export const defaulted:(n:number,x?:number)=>number=(n:number,x:number=1)=>n+x;',
      'export const optional:(n:number,x?:number)=>number=(n:number,x?:number)=>n;',
      'export const restSum=(base:number,...values:number[]):number=>{let total:number=base;for(const item of values){total+=item;}return total;};',
      'export const restCount=(...values:number[]):number=>values.length;'
    ].join('\n'),
    'bridge.ts':'export {inc,apply,defaulted,optional,restSum,restCount} from "./logic.js";',
    'entry.ts':[
      'import type {Callback} from "./model.js";',
      'import {inc,apply,defaulted,optional,restSum,restCount} from "./bridge.js";',
      'const twice:Callback=(n:number):number=>n+n;',
      'const block:Callback=(n:number):number=>{if(n>0){return n+n;}return 0;};',
      'const inferred:Callback=(n:number)=>{if(n>0){return n+n;}return 0;};',
      'export const inferredResult:number=apply(inferred,21);',
      'export const blocked:number=apply(block,21);',
      'export const result:number=apply(twice,21);',
      'export const captured:number=inc(41);',
      'export const defaultedResult:number=defaulted(41);',
      'export const optionalResult:number=optional(42);',
      'export const restResult:number=restSum(20,21,1);',
      'export const restLength:number=restCount(1,2,3);',
      'export const emptyRest:number=restCount();',
      'const bracedDefault=(n:number=21)=>{return n+n;};',
      'export const bracedDefaultResult:number=bracedDefault();'
    ].join('\n')
  };
  for(const [file,code] of Object.entries(files))writeFileSync(join(dir,file),code+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const r=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing JS module '+name);
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,captured,blocked,inferredResult,defaultedResult,optionalResult,bracedDefaultResult,restResult,restLength,emptyRest} from "./entry.js"; if(result!==42||captured!==42||blocked!==42||inferredResult!==42||defaultedResult!==42||optionalResult!==42||bracedDefaultResult!==42||restResult!==42||restLength!==3||emptyRest!==0)throw Error("typed arrow runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const negatives:[string,string,string,number][]=[
    ['return','const bad=(n:number):number=>"no";','TS2322',2],
    ['argument','const fn=(n:number)=>n;const result:number=fn("wrong");','TS2345',2],
    ['arity','const fn=(n:number)=>n;const result:number=fn();','TS2554',2],
    ['assignment','const fn=(n:number)=>n;let bad:(n:string)=>number=fn;','TS2322',2],
    ['missing','const fn=(n:number)=>missing;','TS2304',2],
    ['untyped','const fn=(n)=>n;','TSCS1001',1],
    ['braced-return','const fn=(n:number):number=>{return "bad";};','TS2322',2],
    ['braced-argument','const fn=(n:number):number=>{return n;};const x:number=fn("bad");','TS2345',2],
    ['braced-missing','const fn=(n:number):number=>{return missing;};','TS2304',2],
    ['braced-incomplete','const fn=(n:number):number=>{if(n>0){return n;}};','TSCS1001',1],
    ['braced-partial','const fn=(n:number)=>{if(n>0){return n;}};','TSCS1001',1],
    ['braced-bare','const fn=(n:number):number=>{return;};','TSCS1001',1],
    ['default-type','const f=(n:number="bad")=>n;','TS2322',2],
    ['default-boolean','const f=(n:number=true)=>n;','TS2322',2],
    ['default-argument','const f=(n:number,x:number=1)=>n+x;f(1,"bad");','TS2345',2],
    ['default-missing','const f=(n:number,x:number=1)=>n+x;f();','TS2554',2],
    ['default-extra','const f=(n:number,x:number=1)=>n+x;f(1,2,3);','TS2554',2],
    ['default-optional-conflict','const f=(n?:number=1)=>n;','TS1015',2],
    ['default-nontrailing','const f=(n:number=1,x:number)=>n+x;','TSCS1001',1],
    ['rest-nonarray','const f=(...values:number):number=>42;','TS2370',2],
    ['rest-optional','const f=(...values?:number[]):number=>42;','TS1047',2],
    ['rest-default','const f=(...values:number[]=[]):number=>42;','TS1048',2],
    ['rest-nontrailing','const f=(...values:number[],x:number):number=>42;','TS1014',2],
    ['rest-argument','const f=(...values:number[]):number=>values.length;f("bad");','TS2345',2]
  ];
  for(const [id,source,code,exit] of negatives) {
    const path=join(dir,'invalid-'+id+'.ts');
    writeFileSync(path,source);
    const result=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(result.status,exit,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid arrow emitted JS');
  }
  console.log('typed arrows: four-module ES2015 baseline and defaulted/optional results=42; twenty-four invalid no-emit programs rejected.');
} finally {rmSync(dir,{recursive:true,force:true});}
