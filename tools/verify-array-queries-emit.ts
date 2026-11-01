import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Linked ES2015 execution and rejected-program no-output checks; not upstream acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-array-queries-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]) {
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
    {cwd:folder,encoding:'utf8',timeout:30_000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-queries/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Readings=ReadonlyArray<number>;export const values:number[]=[19,20,21,19];',
    'logic.ts':'import type {Readings} from "./model.js";export function contains(xs:Readings,n:number):boolean{return xs.includes(n);}export function first(xs:Readings,n:number):number{return xs.indexOf(n);}export function last(xs:Readings,n:number):number{return xs.lastIndexOf(n);}export function after(xs:Readings,n:number):number{return xs.indexOf(n,1);}export function label(xs:Readings):string{return xs.join("/");}export function middle(xs:Readings):number[]{return xs.slice(1,-1);}export function copy(xs:Readings):number[]{return xs.slice();}',
    'bridge.ts':'export {values} from "./model.js";export {contains,first,last,after,label,middle,copy} from "./logic.js";',
    'entry.ts':'import {values,contains,first,last,after,label,middle,copy} from "./bridge.js";export const found:boolean=contains(values,21);export const firstIndex:number=first(values,19);export const lastIndex:number=last(values,19);export const laterIndex:number=after(values,19);export const joined:string=label(values);export const sliced:number[]=middle(values);export const writable:number[]=copy(values);writable[0]=99;export const original:number=values[0];'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {found,firstIndex,lastIndex,laterIndex,joined,sliced,writable,original} from "./entry.js"; if(found!==true||firstIndex!==0||lastIndex!==3||laterIndex!==3||joined!=="19/20/21/19"||sliced.length!==2||sliced[0]!==20||sliced[1]!==21||writable[0]!==99||original!==19)throw Error(JSON.stringify({found,firstIndex,lastIndex,laterIndex,joined,sliced,writable,original}));'],
    {cwd:folder,encoding:'utf8',timeout:15_000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected:[string,string,string][]=[
    ['wrong-includes','let xs:number[]=[21];xs.includes("bad");','TS2345'],
    ['missing-includes','let xs:number[]=[21];xs.includes();','TS2554'],
    ['wrong-start','let xs:number[]=[21];xs.includes(21,"bad");','TS2345'],
    ['extra-includes','let xs:number[]=[21];xs.includes(21,0,2);','TS2554'],
    ['missing-index','let xs:number[]=[21];xs.indexOf();','TS2554'],
    ['wrong-last','let xs:readonly number[]=[21];xs.lastIndexOf("bad");','TS2345'],
    ['wrong-separator','let xs:readonly number[]=[21];xs.join(42);','TS2345'],
    ['extra-join','let xs:number[]=[21];xs.join(",",",");','TS2554'],
    ['bad-result','let xs:number[]=[21];let b:boolean=xs.indexOf(21);','TS2322'],
    ['readonly-mutation','let xs:ReadonlyArray<number>=[21];xs.push(22);','TS2339'],
    ['bad-slice-start','let xs:number[]=[21];xs.slice("bad");','TS2345'],
    ['bad-slice-end','let xs:readonly number[]=[21];xs.slice(0,"bad");','TS2345'],
    ['bad-slice-arity','let xs:number[]=[21];xs.slice(0,1,2);','TS2554'],
    ['bad-slice-result','let xs:ReadonlyArray<number>=[21];let n:number=xs.slice(0);','TS2322'],
    ['bad-slice-element','let xs:readonly string[]=["a"];let n:number[]=xs.slice();','TS2322']
  ];
  for(const [name,source,expected] of rejected) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(expected),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('array queries: four linked ES2015 modules executed; fifteen invalid programs blocked output.');
} finally {rmSync(folder,{recursive:true,force:true});}
