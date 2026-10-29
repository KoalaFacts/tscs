import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
const dir=mkdtempSync(join(tmpdir(),'tscs-interface-heritage-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  for(const [name,source] of Object.entries({
    'model.ts':'export interface Base{readonly value:number}export interface Child extends Base{extra:number}export const seed:Child={value:20,extra:22};',
    'logic.ts':'import type {Child} from "./model.js";export function sum(data:Child):number{return data.value+data.extra;}',
    'bridge.ts':'export {seed} from "./model.js";export {sum} from "./logic.js";',
    'entry.ts':'import type {Child} from "./model.js";import {seed,sum} from "./bridge.js";const pair:Child=seed;export const result:number=sum(pair);'
  }))writeFileSync(join(dir,name),source);
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');return result;
  }
  const compiled=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])assert.ok(existsSync(join(dir,name+'.js')));
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result} from "./entry.js";if(result!==42)throw Error("expected 42, got "+result);'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid:[string,string,string,number][]=[
    ['wrong-value','interface A{value:number}interface B extends A{}let b:B={value:"bad"};','TS2322',2],
    ['readonly-parent','interface A{readonly value:number}interface B extends A{}let b:B={value:1};b.value=2;','TS2540',2],
    ['wrong-key','interface A{value:number}interface B extends A{}let k:keyof B="missing";','TS2322',2],
    ['override','interface A{value:number}interface B extends A{value:string}','TS2430',2],
    ['missing-base','interface B extends Missing{value:number}','TS2304',2],
    ['primitive-base','type Num=number;interface B extends Num{value:number}','TSCS1001',1],
    ['cycle','interface A extends B{}interface B extends A{}','TS2456',2]
  ];
  for(const [name,source,code,exit] of invalid){
    const path=join(dir,'bad-'+name+'.ts');writeFileSync(path,source);
    const result=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(result.status,exit,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid source emitted JavaScript');
  }
  console.log('finite interface heritage: four ES2015 modules emitted, result=42; seven invalid programs rejected.');
} finally {rmSync(dir,{recursive:true,force:true});}
