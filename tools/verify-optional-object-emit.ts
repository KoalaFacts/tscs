import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual emitted JavaScript and error/no-emit checks, not original TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-optional-object-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Option { value?: number; label: string }',
    'data.ts':'import type {Option} from "./model.js"; export const absent:Option={label:"empty"}; export const present:Option={value:42,label:"ready"};',
    'logic.ts':'import type {Option} from "./model.js"; export function setValue(x:Option):number{x.value=42;return 42;}',
    'entry.ts':'import {absent,present} from "./data.js"; import {setValue} from "./logic.js"; export const result:number=setValue(absent); export const other:number=setValue(present); export const absentValue:number|undefined=absent.value;'
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]){
    const output=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(output.error,undefined,String(output.error));
    assert.equal(output.signal,null,'compiler timed out or crashed');
    return output;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','data','logic','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing emitted '+name);
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,other,absentValue} from "./entry.js"; import {absent,present} from "./data.js"; if(result!==42||other!==42||absentValue!==42||absent.value!==42||present.value!==42)throw Error("optional data runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['bad-value','let o:{value?:number}={value:"bad"};','TS2322',2],
    ['missing-required','let o:{value?:number}={};let x:{value:number}=o;','TS2741',2],
    ['unsafe-read','let o:{value?:number}={};let n:number=o.value;','TS2322',2],
    ['wrong-write','let o:{value?:number}={};o.value="bad";','TS2322',2],
    ['readonly','let o:{readonly value?:number}={};o.value=42;','TS2540',1],
    ['excess','let o:{value?:number}={extra:42};','TS2353',2],
    ['optional-method','interface Option{run?():number}','TSCS1001',1],
    ['invalid-extension','interface Root{value:number}interface Child extends Root{value?:number}','TS2430',1]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'invalid-'+id+'.ts');writeFileSync(path,source);
    const output=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(output.status,exit,output.stdout+output.stderr);
    assert.match(output.stdout+output.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid emitted JavaScript: '+id);
  }
  console.log('optional object data: four-module real Node output and eight no-emit errors verified');
} finally {rmSync(dir,{recursive:true,force:true});}
