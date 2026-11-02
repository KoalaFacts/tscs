import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Checked four-module runtime and negative no-JS output; not full TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-array-in-place-'));
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
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-in-place/splice-minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Samples=number[];export const values:Samples=[3,1,2,4];',
    'logic.ts':'import type {Samples} from "./model.js";export function order(xs:Samples):Samples{return xs.sort((a:number,b:number):number=>a-b);}export function reverse(xs:Samples):Samples{return xs.reverse();}export function fill(xs:Samples):Samples{return xs.fill(9,1,3);}export function move(xs:Samples):Samples{return xs.copyWithin(0,2,4);}export function cut(xs:Samples):Samples{return xs.splice(1,1,7,8);}',
    'bridge.ts':'export {values} from "./model.js";export {order,reverse,fill,move,cut} from "./logic.js";',
    'entry.ts':'import {values,order,reverse,fill,move,cut} from "./bridge.js";export const ordered:number[]=order(values);export const reversed:number[]=reverse(values);export const filled:number[]=fill(values);export const moved:number[]=move(values);export const removed:number[]=cut(values);export const first:number=values[0];'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {ordered,reversed,filled,moved,removed,first} from "./entry.js";import {values} from "./model.js";if(ordered!==values||reversed!==values||filled!==values||moved!==values||removed===values||removed.join(",")!=="1"||values.join(",")!=="9,7,8,9,1"||first!==9)throw Error(JSON.stringify({values,first}));'],
    {cwd:folder,encoding:'utf8',timeout:15_000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected:[string,string,string][]=[
    ['readonly-reverse','let xs:readonly number[]=[2,1];xs.reverse();','TS2339'],
    ['readonly-sort','let xs:ReadonlyArray<number>=[2,1];xs.sort();','TS2339'],
    ['readonly-fill','let xs:readonly number[]=[2,1];xs.fill(0);','TS2339'],
    ['readonly-copy','let xs:ReadonlyArray<number>=[2,1];xs.copyWithin(0,1);','TS2339'],
    ['reverse-arity','let xs:number[]=[2,1];xs.reverse(1);','TS2554'],
    ['sort-callback','let xs:number[]=[2,1];xs.sort(42);','TS2345'],
    ['fill-value','let xs:number[]=[2,1];xs.fill("bad");','TS2345'],
    ['fill-missing','let xs:number[]=[2,1];xs.fill();','TS2554'],
    ['fill-start','let xs:number[]=[2,1];xs.fill(0,"bad");','TS2345'],
    ['copy-missing','let xs:number[]=[2,1];xs.copyWithin(0);','TS2554'],
    ['copy-target','let xs:number[]=[2,1];xs.copyWithin("bad",1);','TS2345'],
    ['copy-arity','let xs:number[]=[2,1];xs.copyWithin(0,1,2,3);','TS2554'],
    ['wrong-return','let xs:string[]=["a"];let result:number[]=xs.reverse();','TS2322'],
    ['splice-1',"let xs:readonly number[]=[2,1];xs.splice(0,1);","TS2339"],
    ['splice-2',"let xs:ReadonlyArray<number>=[2,1];xs.splice(0,1);","TS2339"],
    ['splice-3',"let xs:number[]=[2,1];xs.splice(\"bad\",1);","TS2345"],
    ['splice-4',"let xs:number[]=[2,1];xs.splice(0,\"bad\");","TS2345"],
    ['splice-5',"let xs:number[]=[2,1];xs.splice(0,1,\"bad\");","TS2345"],
    ['splice-6',"let xs:number[]=[2,1];xs.splice(0,undefined,3);","TS2345"],
    ['splice-7',"let xs:number[]=[2,1];let wrong:string[]=xs.splice(0,1);","TS2322"],
    ['splice-8',"let xs:string[]=[\"a\"];xs.splice(0,0,42);","TS2345"]
  ];
  for(const [name,source,expected] of rejected) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(expected),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('in-place arrays: four ES2015 modules executed, alias mutation checked; twenty-one invalid programs blocked JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
