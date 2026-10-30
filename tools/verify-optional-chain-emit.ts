import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual ES2015 multi-module output; not original TS7 harness acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-optional-chain-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Option { value:number; nested?:{value:number} }',
    'data.ts':[
      'import type {Option} from "./model.js";',
      'export let reads:number=0;',
      'export function make(present:boolean):Option|undefined {',
      '  reads+=1;',
      '  return present ? {value:21,nested:{value:42}} : undefined;',
      '}'
    ].join('\n'),
    'logic.ts':[
      'import type {Option} from "./model.js";',
      'export function twice(o:Option|undefined):number{return (o?.value ?? 21)*2;}',
      'export function inner(o:Option|undefined):number{return o?.nested?.value ?? 42;}',
      'export function keyed(o:Option|undefined):number{return (o?.["value"] ?? 21)*2;}',
      'export function first(o:number[]|undefined):number{return o?.[0] ?? 42;}'
    ].join('\n'),
    'entry.ts':[
      'import {make,reads} from "./data.js";',
      'import {twice,inner,keyed,first} from "./logic.js";',
      'export const present:number=twice(make(true));',
      'export const absent:number=twice(make(false));',
      'export const nested:number=inner(make(true));',
      'export const missingNested:number=inner(make(false));',
      'export const byKey:number=keyed({value:21});',
      'export const byIndex:number=first([42]);',
      'export const missingIndex:number=first(undefined);',
      'export const callCount:number=reads;'
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
  function compile(args:string[]) {
    const out=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(out.error,undefined,String(out.error));
    assert.equal(out.signal,null,'compiler timed out or crashed');
    return out;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','data','logic','entry']) {
    const path=join(dir,name+'.js');
    assert.ok(existsSync(path),'missing output: '+name);
    assert.doesNotMatch(readFileSync(path,'utf8'),/\?\./,'ES2015 must lower optional member access');
  }
  assert.match(readFileSync(join(dir,'logic.js'),'utf8'),/=== undefined/,
    'missing guarded member access');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {present,absent,nested,missingNested,byKey,byIndex,missingIndex,callCount} from "./entry.js"; if([present,absent,nested,missingNested,byKey,byIndex,missingIndex].some(n=>n!==42)||callCount!==4)throw Error("optional member access runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['bad-type','function bad(o:{value:number}|undefined):string{return o?.value ?? 42;}','TS2322',2],
    ['missing','function bad(o:{value:number}|undefined):number{return o?.missing ?? 42;}','TS2339',2],
    ['bad-index','function bad(o:{value:number}|undefined):number{return o?.["missing"] ?? 42;}','TS2339',2],
    ['unsafe','function bad(o:{value:number}|undefined):number{return o.value;}','TS2339',2],
    ['target','let o:{value:number}|undefined={value:21};o?.value=42;','TSCS1001',1],
    ['compound','let o:{value:number}|undefined={value:21};o?.value+=1;','TSCS1001',1],
    ['postfix','let o:{value:number}|undefined={value:21};o?.value++;','TSCS1001',1],
    ['optional-call','function bad(o:{value:number}|undefined):number{return o?.(21);}','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid) {
    const path=join(dir,'bad-'+id+'.ts');
    writeFileSync(path,source);
    const out=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(out.status,exit,out.stdout+out.stderr);
    assert.match(out.stdout+out.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid emitted JS for '+id);
  }
  console.log('optional member/index: four-module ES2015 runtime and eight fail-closed no-emit cases');
} finally {rmSync(dir,{recursive:true,force:true});}
