import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Runs linked emitted ES2015; no original TS7 acceptance credit.
const dir=mkdtempSync(join(tmpdir(),'tscs-string-forof-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]) {
  const result=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{
    cwd:dir,env,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024
  });
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'tscs crashed or timed out');
  assert.notEqual(result.status,null);
  return result;
}
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  const minimal=readFileSync('fixtures/string-iteration/minimal.d.ts','utf8');
  writeFileSync(join(dir,'minimal.d.ts'),minimal);
  const modules={
    'model.ts':'export type Word=string;export type Mixed=string|number[];export const word:Word="A😀B";export const digits:number[]=[1,2,3];',
    'logic.ts':'import type {Word,Mixed} from "./model.js";export function echo(s:Word):string{let result:string="";for(const ch of s){result=result+ch;}return result;}export function count(s:Word):number{let n:number=0;for(const ch of s){n++;}return n;}export function omitEmoji(s:Word):string{let out:string="";for(const ch of s){if(ch==="😀"){continue;}out=out+ch;if(ch==="B"){break;}}return out;}export function renderMix(input:Mixed):string{let out:string="";for(const part of input){let checked:string|number=part;out=out+part;}return out;}export function countMix(input:Mixed):number{let count:number=0;for(const part of input){count++;}return count;}',
    'bridge.ts':'export {word,digits} from "./model.js";export {echo,count,omitEmoji,renderMix,countMix} from "./logic.js";',
    'entry.ts':'import {word,digits,echo,count,omitEmoji,renderMix,countMix} from "./bridge.js";export const countUnicode:number=count(word);export const copied:string=echo(word);export const withoutEmoji:string=omitEmoji(word);export const source:string=word;export const mixedText:string=renderMix(word);export const mixedDigits:string=renderMix(digits);export const mixedStringCount:number=countMix(word);export const mixedArrayCount:number=countMix(digits);'
  };
  for(const [name,content] of Object.entries(modules))writeFileSync(join(dir,name),content);
  const built=compile(['--emitJs','--noLib','--target','es2015','--module','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing linked output '+name);
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {countUnicode,copied,withoutEmoji,source,mixedText,mixedDigits,mixedStringCount,mixedArrayCount} from "./entry.js";if(countUnicode!==3||copied!=="A😀B"||withoutEmoji!=="AB"||source!=="A😀B"||mixedText!=="A😀B"||mixedDigits!=="123"||mixedStringCount!==3||mixedArrayCount!==3)throw Error(JSON.stringify({countUnicode,copied,withoutEmoji,source,mixedText,mixedDigits,mixedStringCount,mixedArrayCount}));'
  ],{cwd:dir,encoding:'utf8',timeout:15000,maxBuffer:1024*1024});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+'\n'+run.stderr);

  const invalid:[string,string,string][]=[
    ['const-rebind','for(const ch of "ab"){ch="x";}','TS2588'],
    ['bad-element','for(const ch of "ab"){let wrong:number=ch;}','TS2322'],
    ['noniterable','for(const ch of 42){let value:number=ch;}','TSCS1001'],
    ['unsafe-union','function f(s:string|undefined):number{for(const ch of s){return 1;}return 0;}','TSCS1001'],
    ['bad-string-member','for(const ch of "ab"){ch.missing;}','TS2339'],
    ['bad-union-element','function f(xs:number[]|string):void{for(const part of xs){let n:number=part;}}','TS2322'],
    ['bad-union-assignment','function f(xs:number[]|string):void{for(let part of xs){part=false;}}','TS2322'],
    ['bad-union-source','function f(xs:string|undefined):void{for(const part of xs){let s:string=part;}}','TSCS1001'],
    ['bad-array-union-source','function f(xs:number[]|undefined):void{for(const part of xs){let n:number=part;}}','TSCS1001']
  ];
  for(const [name,content,expected] of invalid) {
    writeFileSync(join(dir,name+'.ts'),content);
    const r=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.notEqual(r.status,0,name+': '+r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(expected));
    assert.equal(existsSync(join(dir,name+'.js')),false,'invalid code emitted '+name);
  }
  const malformed=minimal.replace('[Symbol.iterator](): Iterator<string>',
    '[Symbol.iterator](): Iterator<number>');
  assert.notEqual(malformed,minimal);
  writeFileSync(join(dir,'malformed.d.ts'),malformed);
  writeFileSync(join(dir,'valid-string.ts'),'for(const c of "ab"){let s:string=c;}');
  const rejected=compile(['--emitJs','--noLib','--target','es2015','valid-string.ts','malformed.d.ts']);
  assert.notEqual(rejected.status,0);
  assert.match(rejected.stdout+rejected.stderr,/TSCS1002/);
  assert.equal(existsSync(join(dir,'valid-string.js')),false);
  console.log('String for..of: four linked ES2015 modules executed, Unicode scalar iteration, nine invalid programs and malformed ambient blocked.');
} finally {
  rmSync(dir,{recursive:true,force:true});
}
