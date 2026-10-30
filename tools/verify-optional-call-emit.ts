import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual checked four-module ES2015 output. Not original TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-optional-call-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export type F=(n:number)=>number;export interface Box{run?(n:number):number;}',
    'data.ts':[
      'import type {F,Box} from "./model.js";',
      'export let created:number=0;',
      'export let argumentsUsed:number=0;',
      'export function next():number{argumentsUsed+=1;return 21;}',
      'export function make(present:boolean):F|undefined{',
      ' created+=1;',
      ' return present ? (n:number):number=>n+n : undefined;',
      '}',
      'export function box(present:boolean):Box{',
      ' return present ? {run(n:number):number{return n+n;}} : {};',
      '}'
    ].join('\n'),
    'logic.ts':[
      'import type {F,Box} from "./model.js";',
      'import {next} from "./data.js";',
      'export function invoke(fn:F|undefined):number{return fn?.(next()) ?? 42;}',
      'export function bound(b:Box):number{return b.run?.(21) ?? 42;}',
      'export function indexed(b:Box):number{return b["run"]?.(21) ?? 42;}'
    ].join('\n'),
    'entry.ts':[
      'import {make,box,created,argumentsUsed} from "./data.js";',
      'import {invoke,bound,indexed} from "./logic.js";',
      'export const missing:number=invoke(make(false));',
      'export const present:number=invoke(make(true));',
      'export const member:number=bound(box(true));',
      'export const absentMember:number=bound(box(false));',
      'export const index:number=indexed(box(true));',
      'export const missingIndex:number=indexed(box(false));',
      'export const builds:number=created;',
      'export const argumentEvaluations:number=argumentsUsed;'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(sources))
    writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',
    TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]){
    const run=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(run.error,undefined,String(run.error));
    assert.equal(run.signal,null,'compiler crashed or timed out');
    return run;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','data','logic','entry']){
    const path=join(dir,name+'.js');
    assert.ok(existsSync(path),'missing JS output '+name);
    assert.doesNotMatch(readFileSync(path,'utf8'),/\?\./,
      'ES2015 must lower optional calls');
  }
  const emitted=readFileSync(join(dir,'logic.js'),'utf8');
  assert.match(emitted,/=== undefined/,'optional functions must be guarded');
  assert.match(emitted,/\.call\(/,'member calls must preserve their receiver');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {missing,present,member,absentMember,index,missingIndex,builds,argumentEvaluations} from "./entry.js";',
    'import {bound,indexed} from "./logic.js";',
    'if([missing,present,member,absentMember,index,missingIndex].some(v=>v!==42))throw Error("optional call mismatch");',
    'if(builds!==2||argumentEvaluations!==1)throw Error("optional-call lazy args or single-evaluation violated");',
    'const self={bias:21,run:function(n){return n+this.bias}};',
    'if(bound(self)!==42||indexed(self)!==42)throw Error("optional member this binding lost");'
  ].join('\n')],{cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['return','type F=(n:number)=>number;function f(g:F|undefined):string{return g?.(21) ?? 42;}','TS2322',2],
    ['argument','type F=(n:number)=>number;function f(g:F|undefined):number{return g?.("wrong") ?? 42;}','TS2345',2],
    ['arity','type F=(n:number)=>number;function f(g:F|undefined):number{return g?.() ?? 42;}','TS2554',2],
    ['member-argument','interface Box{run?(n:number):number;}function f(b:Box):number{return b.run?.("wrong") ?? 42;}','TS2345',2],
    ['not-callable','function f(n:number):number{return n?.(21) ?? 42;}','TSCS1001',1],
    ['chained','function f(b:{run:(n:number)=>number}|undefined):number{return b?.run?.(21) ?? 42;}','TSCS1001',1],
    ['continued','type F=(n:number)=>{value:number};function f(g:F|undefined):number{return g?.(21).value ?? 42;}','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'bad-'+id+'.ts');
    writeFileSync(path,source);
    const run=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(run.status,exit,run.stdout+run.stderr);
    assert.match(run.stdout+run.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid emitted '+id);
  }
  console.log('optional calls: four-module ES2015 execution, receiver binding, lazy arguments and seven no-emit errors');
}finally{rmSync(dir,{recursive:true,force:true});}
