import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module ES2015 runtime, plus invalid/no-JS checks; not TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-string-extraction-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]){
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{
    cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env
  });
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler timed out or crashed');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  const minimal=readFileSync('fixtures/string-extraction/minimal.d.ts','utf8');
  writeFileSync(join(folder,'minimal.d.ts'),minimal);
  const modules={
    'model.ts':'export interface Label { text:string } export const sample:Label={text:"A,B,C"};',
    'logic.ts':'import type {Label} from "./model.js";export function pieces(x:Label):string[]{return x.text.split(",");}export function replace(x:Label):string{return x.text.replace("B","Bee").toLowerCase();}export function position(x:Label):number{return x.text.indexOf("B")+x.text.lastIndexOf("C")+x.text.charCodeAt(0);}export function extract(x:Label):string{return x.text.substring(2,3)+x.text.charAt(0).repeat(2);}',
    'bridge.ts':'export {pieces,replace,position,extract} from "./logic.js";',
    'entry.ts':'import {sample} from "./model.js";import {pieces,replace,position,extract} from "./bridge.js";export const words:string[]=pieces(sample);words[0]="Z";export const joined:string=words.join("-");export const replaced:string=replace(sample);export const index:number=position(sample);export const fragment:string=extract(sample);'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),name+' did not emit JavaScript');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {words,joined,replaced,index,fragment} from "./entry.js";if(JSON.stringify(words)!==JSON.stringify(["Z","B","C"])||joined!=="Z-B-C"||replaced!=="a,bee,c"||index!==71||fragment!=="BAA")throw Error(JSON.stringify({words,joined,replaced,index,fragment}));'
  ],{cwd:folder,encoding:'utf8',timeout:15000,maxBuffer:4*1024*1024});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const cohort=JSON.parse(readFileSync('fixtures/string-extraction/semantic-cases.json','utf8'));
  const invalid=cohort.cases.filter((c:{id:string})=>c.id.includes(':negative-'));
  assert.equal(invalid.length,14);
  for(let i=0;i<invalid.length;i++){
    const entry=invalid[i],name='invalid-'+i;
    writeFileSync(join(folder,name+'.ts'),entry.source);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(rejected.status,2,name+': '+rejected.stdout+rejected.stderr);
    for(const code of entry.expectedCodes)
      assert.match(rejected.stdout+rejected.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid emitted JS: '+name);
  }
  const invalidAmbient=[
    minimal.replace('  split(separator: string, limit?: number): string[];',
      '  split(separator: string, limit?: number): number[];'),
    minimal.replace('  split(separator: string, limit?: number): string[];',
      '  split(separator: string, limit?: number): string[];\n  split(separator: string, limit?: number): string[];'),
    minimal.replace('  repeat(count: number): string;',
      '  imaginary(count: number): string;')
  ];
  for(let i=0;i<invalidAmbient.length;i++){
    const name='ambient-'+i;
    writeFileSync(join(folder,name+'.d.ts'),invalidAmbient[i]);
    writeFileSync(join(folder,name+'.ts'),'const parts:string[]="a,b".split(",");');
    const rejected=compile(['--emitJs','--noLib','--target','es2015',name+'.ts',name+'.d.ts']);
    assert.equal(rejected.status,1,name+': '+rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,/TSCS1002/,name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'bad ambient emitted JS');
  }
  console.log('String extraction: four modules executed, 14 typed negatives and 3 malformed ambient contracts rejected.');
} finally { rmSync(folder,{recursive:true,force:true}); }
