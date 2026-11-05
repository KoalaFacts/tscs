import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {runInNewContext} from 'node:vm';
const folder=mkdtempSync(join(tmpdir(),'tscs-ambient-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(...sources:string[]){
 const r=spawnSync(process.env.DOTNET??'dotnet',
   [dll,'--emitJs','--noLib','--target','es2015',...sources,'minimal.d.ts'],
   {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
 assert.equal(r.error,undefined,String(r.error));assert.equal(r.signal,null);return r;
}
try {
 writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
 writeFileSync(join(folder,'ambient.ts'),[
   '/// <style requireSemi="on" />','declare function twice(n:number):number;',
   'declare function label(n:number):string;'].join('\n'));
 writeFileSync(join(folder,'entry.ts'),'let answer:number=twice(21); let text:string=label(answer);\n');
 const built=compile('ambient.ts','entry.ts');
 assert.equal(built.status,0,built.stdout+built.stderr);
 const ambientJs=readFileSync(join(folder,'ambient.js'),'utf8');
 assert.equal(ambientJs,'"use strict";\r\n');
 const entryJs=readFileSync(join(folder,'entry.js'),'utf8');
 assert.doesNotMatch(entryJs,/function twice|function label/);
 const observed=runInNewContext(ambientJs+entryJs+'[answer,text]',
   {twice:(n:number)=>n*2,label:(n:number)=>'n:'+n},{timeout:1500});
 assert.equal(observed[0],42);assert.equal(observed[1],'n:42');
 for(const [name,source,code] of [
   ['wrong-arg','declare function twice(n:number):number; let r=twice("bad");','TS2345'],
   ['wrong-type','declare function label(n:number):string; let n:number=label(1);','TS2322'],
   ['bad-decl','declare const value:number;','TSCS1001']
 ] as const){
   writeFileSync(join(folder,name+'.ts'),source);
   const r=compile(name+'.ts');
   assert.notEqual(r.status,0,r.stdout+r.stderr);
   assert.match(r.stdout+r.stderr,new RegExp(code));
   assert.equal(existsSync(join(folder,name+'.js')),false);
 }
 console.log('Ambient declarations: 2 source units execute 42, invalid programs emit no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
