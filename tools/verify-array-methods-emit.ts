import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four real modules compiled to native ES2015 and executed by Node.
// The checker must reject invalid methods without emitting JavaScript.
const folder=mkdtempSync(join(tmpdir(),'tscs-array-methods-'));
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
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-methods/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Numbers=Array<number>;export const values:Numbers=[20,21];',
    'logic.ts':'import type {Numbers} from "./model.js";export function append(xs:Numbers,n:number):number{return xs.push(n);} export function take(xs:Numbers):number|undefined{return xs.shift();} export function pull(xs:Numbers):number|undefined{return xs.pop();} export function prefix(xs:Numbers,n:number):number{return xs.unshift(n);}',
    'bridge.ts':'export {values} from "./model.js";export {append,take,pull,prefix} from "./logic.js";',
    'entry.ts':'import {values,append,take,pull,prefix} from "./bridge.js";export const afterAppend:number=append(values,22);export const afterPrefix:number=prefix(values,19);export const first:number|undefined=take(values);export const last:number|undefined=pull(values);export const remaining:number=values.length;export const stillFirst:number=values[0];'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {afterAppend,afterPrefix,first,last,remaining,stillFirst} from "./entry.js"; if(afterAppend!==3||afterPrefix!==4||first!==19||last!==22||remaining!==2||stillFirst!==20) throw Error(JSON.stringify({afterAppend,afterPrefix,first,last,remaining,stillFirst}));'],
    {cwd:folder,encoding:'utf8',timeout:15_000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected:[string,string,string][]=[
    ['wrong-push','let xs:number[]=[21];xs.push("bad");','TS2345'],
    ['wrong-unshift','let xs:number[]=[21];xs.unshift("bad");','TS2345'],
    ['bad-pop-arity','let xs:number[]=[21];xs.pop(1);','TS2554'],
    ['bad-shift-arity','let xs:number[]=[21];xs.shift(1);','TS2554'],
    ['readonly-push','let xs:readonly number[]=[21];xs.push(22);','TS2339'],
    ['readonly-pop','let xs:ReadonlyArray<number>=[21];xs.pop();','TS2339'],
    ['readonly-unshift','let xs:readonly number[]=[21];xs.unshift(20);','TS2339'],
    ['readonly-shift','let xs:ReadonlyArray<number>=[21];xs.shift();','TS2339'],
    ['bad-result','let xs:number[]=[21];let s:string=xs.push(22);','TS2322']
  ];
  for(const [name,source,expected] of rejected) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(expected),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('array mutation methods: four linked ES2015 modules executed; nine invalid programs blocked output.');
} finally {rmSync(folder,{recursive:true,force:true});}
