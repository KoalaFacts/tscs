import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual linked ES2015 execution and negative no-JS checks (not TS7 acceptance).
const folder=mkdtempSync(join(tmpdir(),'tscs-array-callbacks-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]){
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{cwd:folder,encoding:'utf8',timeout:30_000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try{
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-callbacks/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Readings=ReadonlyArray<number>;export const values:number[]=[19,20,21,22];',
    'logic.ts':"import type {Readings} from \"./model.js\";export function any(xs:Readings):boolean{return xs.some((n:number):boolean=>n>21);}export function all(xs:Readings):boolean{return xs.every((n:number):boolean=>n>0);}export function first(xs:Readings):number|undefined{return xs.find((n:number):boolean=>n>20);}export function index(xs:Readings):number{return xs.findIndex((n:number):boolean=>n>20);}export function total(xs:Readings):number{let sum:number=0;xs.forEach((n:number):void=>{sum=sum+n;});return sum;}",
    'bridge.ts':'export {values} from "./model.js";export {any,all,first,index,total} from "./logic.js";',
    'entry.ts':'import {values,any,all,first,index,total} from "./bridge.js";export const some:boolean=any(values);export const every:boolean=all(values);export const found:number|undefined=first(values);export const position:number=index(values);export const sum:number=total(values);'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {some,every,found,position,sum} from "./entry.js";import {values} from "./model.js";if(some!==true||every!==true||found!==21||position!==2||sum!==82||values.join(",")!=="19,20,21,22")throw Error(JSON.stringify({some,every,found,position,sum,values}));'],
    {cwd:folder,encoding:'utf8',timeout:15_000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected=[["let xs:number[]=[21];xs.every();","TS2554"],["let xs:number[]=[21];xs.some(42);","TS2345"],["let xs:readonly number[]=[21];xs.find((n:string):boolean=>true);","TS2345"],["let xs:number[]=[21];xs.findIndex((n:number):string=>\"bad\");","TS2345"],["let xs:ReadonlyArray<number>=[21];xs.forEach();","TS2554"],["let xs:number[]=[21];xs.forEach((n:string):void=>{});","TS2345"],["let xs:number[]=[21];let wrong:number=xs.every((n:number):boolean=>true);","TS2322"],["let xs:number[]=[21];let wrong:boolean=xs.findIndex((n:number):boolean=>true);","TS2322"],["let xs:number[]=[21];let wrong:number=xs.forEach((n:number):void=>{});","TS2322"],["let xs:number[]=[21];xs.some((n:number):boolean=>true,(n:number):boolean=>true);","TS2554"]];
  for(let i=0;i<rejected.length;i++){
    const [source,code]=rejected[i],name='invalid-'+i;
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('Array callbacks: 4 ES2015 modules executed; 10 invalid programs emitted no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
