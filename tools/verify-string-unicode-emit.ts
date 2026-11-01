import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Executed four-module ES2015 output and no-output negatives; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-string-unicode-'));
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
  const minimal=readFileSync('fixtures/string-unicode/minimal.d.ts','utf8');
  writeFileSync(join(folder,'minimal.d.ts'),minimal);
  const modules={
    'model.ts':'export interface Message { text:string } export const sample:Message={text:"  café  "};',
    'logic.ts':'import type {Message} from "./model.js";export function clean(m:Message):string{return m.text.trimStart().trimEnd().normalize("NFC");}export function decorated(m:Message):string{return clean(m).padStart(7,"_").padEnd(9,"!");}export function point(m:Message):number|undefined{return clean(m).codePointAt(0);}export function compare(m:Message):number{return clean(m).localeCompare("café");}',
    'bridge.ts':'export {clean,decorated,point,compare} from "./logic.js";',
    'entry.ts':'import {sample} from "./model.js";import {clean,decorated,point,compare} from "./bridge.js";export const normalized:string=clean(sample);export const padded:string=decorated(sample);export const cp:number|undefined=point(sample);export const match:number=compare(sample);export const optional:string=clean(sample).padEnd(6,undefined);'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),name+' did not emit JavaScript');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {normalized,padded,cp,match,optional} from "./entry.js";if(normalized!=="café"||padded!=="___café!!"||cp!==99||match!==0||optional!=="café  ")throw Error(JSON.stringify({normalized,padded,cp,match,optional}));'
  ],{cwd:folder,encoding:'utf8',timeout:15000,maxBuffer:4*1024*1024});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const cohort=JSON.parse(readFileSync('fixtures/string-unicode/semantic-cases.json','utf8'));
  const invalid=cohort.cases.filter((c:{id:string})=>c.id.startsWith('string-unicode:negative-'));
  assert.equal(invalid.length,14);
  for(let i=0;i<invalid.length;i++){
    const entry=invalid[i],name='invalid-'+i;
    writeFileSync(join(folder,name+'.ts'),entry.source);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(rejected.status,2,name+': '+rejected.stdout+rejected.stderr);
    for(const code of entry.expectedCodes)assert.match(rejected.stdout+rejected.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid emitted JS: '+name);
  }
  const original='  codePointAt(pos: number): number | undefined;';
  const malformed=[
    minimal.replace(original,'  codePointAt(pos: number): string;'),
    minimal.replace(original,original+'\n'+original),
    minimal.replace(original,'  invented(pos: number): number | undefined;')
  ];
  for(let i=0;i<malformed.length;i++){
    const name='ambient-'+i;
    assert.notEqual(malformed[i],minimal);
    writeFileSync(join(folder,name+'.d.ts'),malformed[i]);
    writeFileSync(join(folder,name+'.ts'),'const n:number|undefined="A".codePointAt(0);');
    const rejected=compile(['--emitJs','--noLib','--target','es2015',name+'.ts',name+'.d.ts']);
    assert.equal(rejected.status,1,name+': '+rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,/TSCS1002/,name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid ambient emitted JS');
  }
  console.log('String Unicode: four linked modules executed, 14 negatives and 3 malformed ambient contracts rejected.');
} finally { rmSync(folder,{recursive:true,force:true}); }
