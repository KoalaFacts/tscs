import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-array-map-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]){
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-map/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Readings=ReadonlyArray<number>;export const values:number[]=[2,3,4];',
    'logic.ts':'import type {Readings} from "./model.js";export function labels(xs:Readings):string[]{return xs.map((n:number):string=>`#${n}`);}export function doubled(xs:Readings):number[]{return xs.map((n:number):number=>n*2);}export function boxed(xs:Readings):{value:number}[]{return xs.map((n:number):{value:number}=>{return {value:n};});}',
    'bridge.ts':'export {values} from "./model.js";export {labels,doubled,boxed} from "./logic.js";',
    'entry.ts':'import {values,labels,doubled,boxed} from "./bridge.js";export const strings:string[]=labels(values);export const twice:number[]=doubled(values);export const objects:{value:number}[]=boxed(values);strings[0]="changed";'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const result=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(result.status,0,result.stdout+result.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {strings,twice,objects} from "./entry.js";import {values} from "./model.js";if(strings.join(",")!=="changed,#3,#4"||twice.join(",")!=="4,6,8"||objects.map(x=>x.value).join(",")!=="2,3,4"||values.join(",")!=="2,3,4")throw Error(JSON.stringify({strings,twice,objects,values}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const negatives=[["let xs:number[]=[21];xs.map();","TS2554"],["let xs:number[]=[21];xs.map(42);","TS2345"],["let xs:number[]=[21];xs.map((s:string):string=>s);","TS2345"],["let xs:number[]=[21];let wrong:number[]=xs.map((n:number):string=>\"x\");","TS2322"],["let xs:readonly number[]=[21];xs.map((s:string):string=>s);","TS2345"],["let xs:number[]=[21];xs.map((n:number):number=>n,42);","TS2554"],["let xs:number[]=[21];let strings:string[]=xs.map((n:number):string=>\"x\");strings[0]=3;","TS2322"],["let xs:number[]=[21];let wrong:boolean[]=xs.map((n:number):number=>n*2);","TS2322"],["function bad(xs:number[]|undefined):number{return xs?.map((n:number):string=>\"x\")[0] ?? 0;}","TS2322"],["let xs:number[]=[1];let f:(cb:(n:number)=>string)=>number[]=xs.map;","TS2322"]];
  for(let i=0;i<negatives.length;i++){
    const [source,code]=negatives[i],name='invalid-'+i;
    writeFileSync(join(folder,name+'.ts'),source);
    const failed=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(failed.status,2,name+': '+failed.stdout+failed.stderr);
    assert.match(failed.stdout+failed.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('Native typed array map: four linked ES2015 modules executed; '+negatives.length+' invalid programs emitted no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
