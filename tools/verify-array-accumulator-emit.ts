import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual native JS execution, typed rejection; no original harness acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-array-accumulator-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]) {
  const r=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.signal,null,'compiler timed out or crashed');
  return r;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-accumulator/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Values=ReadonlyArray<number>;export const values:number[]=[2,3,4];',
    'logic.ts':'import type {Values} from "./model.js";export function join(xs:Values):string{return xs.reduce((a:string,n:number):string=>a+n,"");}export function backwards(xs:Values):string{return xs.reduceRight((a:string,n:number):string=>a+n,"");}export function total(xs:Values):{value:number}{return xs.reduce((a:{value:number},n:number):{value:number}=>({value:a.value+n}),{value:0});}',
    'bridge.ts':'export {values} from "./model.js";export {join,backwards,total} from "./logic.js";',
    'entry.ts':'import {values,join,backwards,total} from "./bridge.js";export const label:string=join(values);export const reverseLabel:string=backwards(values);export const summary:{value:number}=total(values);'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])assert.ok(existsSync(join(folder,name+'.js')));
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {label,reverseLabel,summary} from "./entry.js";import {values} from "./model.js";if(label!=="234"||reverseLabel!=="432"||summary.value!==9||values.join(",")!=="2,3,4")throw Error(JSON.stringify({label,reverseLabel,summary,values}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const suite=JSON.parse(readFileSync('fixtures/array-accumulator/semantic-cases.json','utf8'));
  let rejected=0;
  for(const item of suite.cases.filter((x:{id:string})=>x.id.includes(':negative-'))){
    const name='invalid-'+rejected++;
    writeFileSync(join(folder,name+'.ts'),item.source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    for(const code of item.expectedCodes)assert.match(result.stdout+result.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  assert.equal(rejected,8);
  console.log('Different-type accumulator: four linked modules executed; eight invalid programs emitted no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
