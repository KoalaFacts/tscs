import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Executes actual generated ES2015 across four modules, not original TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-delete-optional-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Mutable {value?:number;run?(n:number):number;label:string}',
    'data.ts':'import type {Mutable} from "./model.js";export const present:Mutable={value:21,run:(n:number)=>n+n,label:"ready"};export const absent:Mutable={label:"empty"};',
    'logic.ts':'import type {Mutable} from "./model.js";export function clearValue(x:Mutable):boolean{return delete x.value;}export function clearRun(x:Mutable):boolean{return delete x["run"]; }',
    'entry.ts':'import {present,absent} from "./data.js";import {clearValue,clearRun} from "./logic.js";export const before:number|undefined=present.value;export const first:boolean=clearValue(present);export const second:boolean=clearValue(absent);export const third:boolean=clearRun(present);export const after:number|undefined=present.value;export const afterCall:number|undefined=present.run?.(21);'
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function compile(args:string[]){
    const result=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{
      cwd:dir,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
      env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler timed out or crashed');
    return result;
  }
  const built=compile(['--emitJs','-p','project.json']);
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','data','logic','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing emitted JavaScript: '+name);
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {before,first,second,third,after,afterCall} from "./entry.js";'+
    'import {present,absent} from "./data.js";'+
    'if(before!==21||first!==true||second!==true||third!==true||after!==undefined||afterCall!==undefined||"value" in present||"run" in present||"value" in absent)throw Error("optional delete runtime mismatch");'
  ],{cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);
  const invalid:[string,string,string,number][]=[
    ['required','let x:{value:number}={value:1};let ok:boolean=delete x.value;','TS2790',2],
    ['readonly','let x:{readonly value?:number}={value:1};let ok:boolean=delete x.value;','TS2704',2],
    ['bare','let x:{value?:number}={value:1};let ok:boolean=delete x;','TSCS1001',1],
    ['literal','let ok:boolean=delete 21;','TSCS1001',1],
    ['optional-chain','let x:{value?:number}={value:1};let ok:boolean=delete x?.value;','TSCS1001',1],
    ['array','let x:number[]=[1,2];let ok:boolean=delete x[0];','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'invalid-'+id+'.ts');writeFileSync(path,source);
    const result=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(result.status,exit,result.stdout+'\n'+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid emitted JavaScript: '+id);
  }
  console.log('delete optional: 4 linked ES2015 modules executed; 6 invalid cases blocked emission');
} finally {rmSync(dir,{recursive:true,force:true});}
