import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Exercise linked native ES2015 output and prevent emitting erroneous sources.
const folder=mkdtempSync(join(tmpdir(),'tscs-array-filter-reduce-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]){
  const r=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{cwd:folder,encoding:'utf8',timeout:30_000,env});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.signal,null,'compiler crashed or timed out');
  return r;
}
try{
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-filter-reduce/minimal.d.ts','utf8'));
  const modules={"model.ts":"export type Values=ReadonlyArray<number>;export const values:number[]=[2,3,4,5];","logic.ts":"import type {Values} from \"./model.js\";export function evens(xs:Values):number[]{return xs.filter((n:number):boolean=>n%2===0);}export function sum(xs:Values):number{return xs.reduce((acc:number,n:number):number=>acc+n,0);}export function backwards(xs:Values):number{return xs.reduceRight((acc:number,n:number):number=>acc*10+n,0);}","bridge.ts":"export {values} from \"./model.js\";export {evens,sum,backwards} from \"./logic.js\";","entry.ts":"import {values,evens,sum,backwards} from \"./bridge.js\";export const selected:number[]=evens(values);export const total:number=sum(values);export const backwardsValue:number=backwards(values);selected[0]=100;"};
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {selected,total,backwardsValue} from "./entry.js";import {values} from "./model.js";if(selected.join(",")!=="100,4"||total!==14||backwardsValue!==5432||values.join(",")!=="2,3,4,5")throw Error(JSON.stringify({selected,total,backwardsValue,values}));'],{cwd:folder,encoding:'utf8',timeout:15_000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected=[["let xs:number[]=[21];xs.filter();","TS2554"],["let xs:number[]=[21];xs.filter(42);","TS2345"],["let xs:readonly number[]=[21];xs.filter((n:string):boolean=>true);","TS2345"],["let xs:number[]=[21];xs.filter((n:number):string=>\"bad\");","TS2345"],["let xs:number[]=[21];let value:boolean=xs.filter((n:number):boolean=>true);","TS2322"],["let xs:number[]=[21];xs.reduce((a:number,n:number):string=>\"bad\",0);","TS2345"],["let xs:number[]=[21];xs.reduce((a:number,n:number):number=>a+n,\"bad\");","TS2345"],["let xs:readonly number[]=[21];xs.reduceRight((a:number,n:number):string=>\"bad\",0);","TS2345"],["let xs:number[]=[21];let value:string=xs.reduceRight((a:number,n:number):number=>a+n,0);","TS2322"],["let xs:number[]=[21];xs.filter((n:number):boolean=>true,42);","TS2554"]];
  for(let i=0;i<rejected.length;i++){
    const [source,code]=rejected[i],name='invalid-'+i;
    writeFileSync(join(folder,name+'.ts'),source);
    const r=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(r.status,2,name+': '+r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('Array filter/folds: four linked ES2015 modules executed; '+rejected.length+' invalid programs emitted no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
