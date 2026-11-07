import {runTscs} from './tscs-process.ts';
import assert from 'node:assert/strict';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {runInNewContext} from 'node:vm';
const folder=mkdtempSync(join(tmpdir(),'tscs-ambient-'));
function compile(...sources:string[]){
 const r=runTscs(folder,['--emitJs','--noLib','--target','es2015','--module','es2015',...sources,'minimal.d.ts']);
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
   ['bad-decl','declare const value:number=1;','TSCS1001'],
   ['duplicate-parameters','declare function f(x:number, x:string):void;','TSCS1001']
 ] as const){
   writeFileSync(join(folder,name+'.ts'),source);
   const r=compile(name+'.ts');
   assert.notEqual(r.status,0,r.stdout+r.stderr);
   assert.match(r.stdout+r.stderr,new RegExp(code));
   assert.equal(existsSync(join(folder,name+'.js')),false);
 }
 // A genuine checked .d.ts unit contributes typed bindings to the real
 // compilation without turning into an emitted JavaScript file.
 writeFileSync(join(folder,'globals.d.ts'),'declare const factor: number; declare function triple(n:number):number;');
 writeFileSync(join(folder,'linked.ts'),'let result: number = triple(factor);');
 const linked=compile('linked.ts','globals.d.ts');
 assert.equal(linked.status,0,linked.stdout+linked.stderr);
 assert.equal(existsSync(join(folder,'globals.js')),false);
 const linkedJs=readFileSync(join(folder,'linked.js'),'utf8');
 assert.equal(runInNewContext(linkedJs+'result',{factor:7,triple:(n:number)=>n*3},{timeout:1500}),21);
 for(const [name,source,code] of [
   ['invalid-call','let result: number = triple("bad");','TS2345'],
   ['invalid-assignment','let result: boolean = triple(7);','TS2322']
 ] as const){
   writeFileSync(join(folder,name+'.ts'),source);
   const outcome=compile(name+'.ts','globals.d.ts');
   assert.equal(outcome.status,2,outcome.stdout+outcome.stderr);
   assert.match(outcome.stdout+outcome.stderr,new RegExp(code));
   assert.equal(existsSync(join(folder,name+'.js')),false);
   assert.equal(existsSync(join(folder,'globals.js')),false);
 }
 console.log('Ambient declarations: checked .d.ts values link across files, execute 21, invalid calls emit no JS.');
 console.log('Ambient declarations: 2 source units execute 42, invalid programs emit no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
