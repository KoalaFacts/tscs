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
      'export function invoke(fn:Fn|null):number{return fn?.(21) ?? 42;}',
      'export function strictRead(m:Maybe):number{if(m===null){return 0;}return m.value*2;}',
      'export function reversedRead(m:Maybe):number{if(null!==m){return m.value;}return 0;}',
      'export function looseRead(m:Maybe|undefined):number{if(m!=null){return m.value;}return 0;}',
      'export function conditionalRead(m:Maybe):number{return m===null?0:m.value;}',
      'export function undefinedGuard(v:{value:number}|undefined):number{if(v===undefined){return 0;}return v.value;}',
      'export function undefinedReverse(v:{value:number}|undefined):number{if(undefined!==v){return v.value;}return 0;}',
      'export function undefinedLoose(v:Maybe|undefined):number{if(v!=undefined){return v.value;}return 0;}',
      'export function undefinedVoid(v:{value:number}|undefined):number{if(v===void 0){return 0;}return v.value;}',
      'export function undefinedTernary(v:{value:number}|undefined):number{return v===undefined?0:v.value;}'
    ].join('\n'),
    'entry.ts':[
      'import {make,none,calls} from "./data.js";',
      'import {read,requirePresent,invoke,strictRead,reversedRead,looseRead,conditionalRead,undefinedGuard,undefinedReverse,undefinedLoose,undefinedVoid,undefinedTernary} from "./logic.js";',
      'export const result:number=read(make(21))*2;',
      'export const missing:number=read(none());',
      'export const present:number=requirePresent(make(21));',
      'export const fallback:number=null ?? 42;',
      'export const callbackMissing:number=invoke(null);',
      'export const callbackPresent:number=invoke((n:number):number=>n+n);',
      'export const totalCalls:number=calls;',
      'export const strictGuard:number=strictRead({value:21});',
      'export const strictMissing:number=strictRead(null);',
      'export const reversedGuard:number=reversedRead({value:21});',
      'export const loosePresent:number=looseRead({value:21});',
      'export const looseMissing:number=looseRead(void 0);',
      'export const ternaryGuard:number=conditionalRead({value:21});',
      'export const undefinedPresent:number=undefinedGuard({value:21});',
      'export const undefinedMissing:number=undefinedGuard(void 0);',
      'export const undefinedReversed:number=undefinedReverse({value:21});',
      'export const undefinedLooseNull:number=undefinedLoose(null);',
      'export const undefinedLoosePresent:number=undefinedLoose({value:21});',
      'export const undefinedVoidMissing:number=undefinedVoid(void 0);',
      'export const undefinedTernaryPresent:number=undefinedTernary({value:21});'
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
    'import {result,missing,present,fallback,callbackMissing,callbackPresent,totalCalls,strictGuard,strictMissing,reversedGuard,loosePresent,looseMissing,ternaryGuard,undefinedPresent,undefinedMissing,undefinedReversed,undefinedLooseNull,undefinedLoosePresent,undefinedVoidMissing,undefinedTernaryPresent} from "./entry.js";',
    'if(result!==42||missing!==42||present!==21||fallback!==42||callbackMissing!==42||callbackPresent!==42||totalCalls!==3||strictGuard!==42||strictMissing!==0||reversedGuard!==21||loosePresent!==21||looseMissing!==0||ternaryGuard!==21||undefinedPresent!==21||undefinedMissing!==0||undefinedReversed!==21||undefinedLooseNull!==0||undefinedLoosePresent!==21||undefinedVoidMissing!==0||undefinedTernaryPresent!==21)',
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
    ['unguarded','let o:{value:number}|null=null;let bad:number=o.value;','TS2339'],
    ['guard-wrong-arm','function bad(m:{value:number}|null):number{if(m===null){return m.value;}return 0;}','TS2339'],
    ['strict-undefined','function bad(m:{value:number}|null|undefined):number{if(m!==null){return m.value;}return 0;}','TS2339'],
    ['guard-rebound','function bad(m:{value:number}|null):number{if(m===null){return 0;}m=null;return m.value;}','TS2339'],
    ['undefined-wrong-arm','function bad(m:{value:number}|undefined):number{if(m===undefined){return m.value;}return 0;}','TS2339'],
    ['undefined-strict-null','function bad(m:{value:number}|null|undefined):number{if(m!==undefined){return m.value;}return 0;}','TS2339'],
    ['undefined-shadow','function bad(m:{value:number}|undefined):number{let undefined:number=0;if(m!==undefined){return m.value;}return 0;}','TS2339']
  ];
  for(const [id,source,code] of invalid){
    const file=join(dir,'invalid-'+id+'.ts');writeFileSync(file,source);
    const r=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(r.status,2,r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(code));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'invalid emitted JS: '+id);
  }
  console.log('explicit null and equality flow: four ES2015 modules executed, strict/loose/undefined guards and twelve invalid no-emit sources');
} finally {rmSync(dir,{recursive:true,force:true});}
