import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Genuine four-module ES2015 runtime check; not full upstream acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-null-literal-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export type Maybe={value:number}|null;export type Fn=(n:number)=>number;',
    'data.ts':[
      'import type {Maybe} from "./model.js";',
      'export let calls:number=0;',
      'export function make(n:number):Maybe {calls+=1;return {value:n};}',
      'export function none():Maybe {calls+=1;return null;}'
    ].join('\n'),
    'logic.ts':[
      'import type {Maybe,Fn} from "./model.js";',
      'export function read(m:Maybe):number{return m?.value ?? 42;}',
      'export function requirePresent(m:Maybe):number{return m!.value;}',
      'export function invoke(fn:Fn|null):number{return fn?.(21) ?? 42;}'
    ].join('\n'),
    'entry.ts':[
      'import {make,none,calls} from "./data.js";',
      'import {read,requirePresent,invoke} from "./logic.js";',
      'export const result:number=read(make(21))*2;',
      'export const missing:number=read(none());',
      'export const present:number=requirePresent(make(21));',
      'export const fallback:number=null ?? 42;',
      'export const callbackMissing:number=invoke(null);',
      'export const callbackPresent:number=invoke((n:number):number=>n+n);',
      'export const totalCalls:number=calls;'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]){
    const r=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env,maxBuffer:4*1024*1024});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','data','logic','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing module '+name);
  assert.match(readFileSync(join(dir,'data.js'),'utf8'),/return null/);
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,missing,present,fallback,callbackMissing,callbackPresent,totalCalls} from "./entry.js";',
    'if(result!==42||missing!==42||present!==21||fallback!==42||callbackMissing!==42||callbackPresent!==42||totalCalls!==3)',
    '  throw Error("null literal runtime, nullable call or single-evaluation regression");'
  ].join('\n')],{cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string][]=[
    ['primitive','let bad:number=null;','TS2322'],
    ['undefined','let bad:undefined=null;','TS2322'],
    ['inverse','let bad:null=21;','TS2322'],
    ['wrong-union','let bad:number|null="bad";','TS2322'],
    ['missing-name','let bad:null=missing;','TS2304'],
    ['unguarded','let o:{value:number}|null=null;let bad:number=o.value;','TS2339']
  ];
  for(const [id,source,code] of invalid){
    const file=join(dir,'invalid-'+id+'.ts');writeFileSync(file,source);
    const r=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(r.status,2,r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(code));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'invalid emitted JS: '+id);
  }
  console.log('explicit null: four ES2015 modules executed, nullable fallbacks/calls and six invalid no-emit sources');
} finally {rmSync(dir,{recursive:true,force:true});}
