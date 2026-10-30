import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Postfix ! is a type-only claim. Exercise actual ES2015 output across modules
// and ensure independent type errors still block emission.
const dir=mkdtempSync(join(tmpdir(),'tscs-non-null-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Option { value?: number; run?(n:number):number; nested?:{factor:number} }',
    'data.ts':[
      'import type {Option} from "./model.js";',
      'export const option:Option={value:21,run:(n:number)=>n+n,nested:{factor:21}};',
      'export let visits:number=0;',
      'export function once():Option {visits+=1;return option;}'
    ].join('\n'),
    'logic.ts':[
      'import type {Option} from "./model.js";',
      'export function choose(o:Option):number{return o.value!*2;}',
      'export function invoke(o:Option):number{return o.run!(21);}',
      'export function nested(o:Option):number{return o.nested!.factor*2;}',
      'export function bracket(o:Option):number{return o["value"]!*2;}'
    ].join('\n'),
    'entry.ts':[
      'import {option,once,visits} from "./data.js";',
      'import {choose,invoke,nested,bracket} from "./logic.js";',
      'export const result:number=choose(option);',
      'export const called:number=invoke(option);',
      'export const deep:number=nested(option);',
      'export const keyed:number=bracket(option);',
      'export const fromOnce:number=once().value!*2;',
      'export const calls:number=visits;'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function run(args:string[]){
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const good=run(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','data','logic','entry']){
    const out=join(dir,name+'.js');
    assert.ok(existsSync(out),'missing emitted '+name);
    assert.doesNotMatch(readFileSync(out,'utf8'),/\\b(?:value|run|nested)!/,'postfix ! must erase');
  }
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,called,deep,keyed,fromOnce,calls} from "./entry.js"; if(result!==42||called!==42||deep!==42||keyed!==42||fromOnce!==42||calls!==1)throw Error("postfix non-null runtime/single evaluation mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['bad-type','let o:{value?:number}={};let s:string=o.value!;','TS2322',2],
    ['missing-member','let o:{value?:number}={};let n:number=o.absent!;','TS2339',2],
    ['bad-argument','let o:{run?:(n:number)=>number}={};o.run!("bad");','TS2345',2],
    ['missing-argument','let o:{run?:(n:number)=>number}={};o.run!();','TS2554',2],
    ['still-optional','let o:{value?:number}={};let n:number=o.value;','TS2322',2],
    ['still-unsafe-call','interface Op{run?(n:number):number}let o:Op={};o.run(21);','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'invalid-'+id+'.ts');
    writeFileSync(path,source);
    const check=run(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(check.status,exit,check.stdout+check.stderr);
    assert.match(check.stdout+check.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\\.ts$/,'.js')),false,'invalid emitted JavaScript: '+id);
  }
  console.log('postfix non-null: real four-module ES2015 runtime, one call evaluation, six fail-closed no-emit cases');
} finally {rmSync(dir,{recursive:true,force:true});}
