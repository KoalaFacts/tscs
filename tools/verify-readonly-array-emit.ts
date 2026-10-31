import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Readonly arrays erase to ordinary ES2015 arrays in the real module pipeline.
const folder=mkdtempSync(join(tmpdir(),'tscs-readonly-array-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]) {
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/readonly-array/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Readings = readonly number[]; export const mutable: number[] = [19,23]; export const view: Readings = mutable;',
    'logic.ts':'import type {Readings} from "./model.js"; export function sum(items:Readings):number { let total:number=0; for (const value of items) { total += value; } return total; } export function first(items:Readings):number { return items[0]; }',
    'bridge.ts':'export {mutable,view} from "./model.js"; export {sum,first} from "./logic.js";',
    'entry.ts':'import {mutable,view,sum,first} from "./bridge.js"; const copy:number[]=[...view]; copy[0]=21; export const result:number=sum(view); export const firstValue:number=first(view); export const sourceValue:number=mutable[0]; export const copyValue:number=copy[0];'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry']) {
    const emitted=join(folder,name+'.js');
    assert.ok(existsSync(emitted),'missing '+emitted);
    assert.doesNotMatch(readFileSync(emitted,'utf8'),/\breadonly\b|\btype Readings\b|\bimport type\b/);
  }
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,firstValue,sourceValue,copyValue} from "./entry.js"; if(result!==42||firstValue!==19||sourceValue!==19||copyValue!==21)throw Error(JSON.stringify({result,firstValue,sourceValue,copyValue}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected:[string,string,string][]=[
    ['index-write','let xs:readonly number[]=[21];xs[0]=22;','TS2542'],
    ['compound-index','let xs:readonly number[]=[21];xs[0]+=1;','TS2542'],
    ['prefix-index','let xs:readonly number[]=[21];++xs[0];','TS2542'],
    ['readonly-length','let xs:readonly number[]=[21];xs.length=0;','TS2540'],
    ['readonly-to-mutable','let xs:readonly number[]=[21];let ys:number[]=xs;','TS4104'],
    ['bad-element-type','let xs:readonly number[]=[21];let s:string=xs[0];','TS2322'],
    ['outer-only','let xs:readonly number[][]=[[21]];xs[0]=[42];','TS2542']
  ];
  for(const [name,source,expected] of rejected){
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(expected),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('readonly arrays: four linked ES2015 modules executed, seven unsafe programs blocked.');
} finally {rmSync(folder,{recursive:true,force:true});}
