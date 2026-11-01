import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Linked ES2015 execution and rejected-program no-output checks; not upstream acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-array-queries-'));
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
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-queries/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export type Readings=ReadonlyArray<number>;export const values:number[]=[19,20,21,19];',
    'logic.ts':'import type {Readings} from "./model.js";export function contains(xs:Readings,n:number):boolean{return xs.includes(n);}export function first(xs:Readings,n:number):number{return xs.indexOf(n);}export function last(xs:Readings,n:number):number{return xs.lastIndexOf(n);}export function after(xs:Readings,n:number):number{return xs.indexOf(n,1);}export function label(xs:Readings):string{return xs.join("/");}export function middle(xs:Readings):number[]{return xs.slice(1,-1);}export function copy(xs:Readings):number[]{return xs.slice();}export function append(xs:Readings,rest:Readings):number[]{return xs.concat(rest,22);}export function atEnd(xs:Readings):number|undefined{return xs.at(-1);}export function outOfRange(xs:Readings):number|undefined{return xs.at(-9);}export function reversedCopy(xs:Readings):number[]{return xs.toReversed();}export function sortedCopy(xs:Readings):number[]{return xs.toSorted((a:number,b:number):number=>a-b);}export function withCopy(xs:Readings):number[]{return xs.with(-1,99);}export function splicedCopy(xs:Readings):number[]{return xs.toSpliced(1,2,41,42);}export function trimmedCopy(xs:Readings):number[]{return xs.toSpliced(2);}',
    'bridge.ts':'export {values} from "./model.js";export {contains,first,last,after,label,middle,copy,append,atEnd,outOfRange,reversedCopy,sortedCopy,withCopy,splicedCopy,trimmedCopy} from "./logic.js";',
    'entry.ts':'import {values,contains,first,last,after,label,middle,copy,append,atEnd,outOfRange,reversedCopy,sortedCopy,withCopy,splicedCopy,trimmedCopy} from "./bridge.js";export const found:boolean=contains(values,21);export const firstIndex:number=first(values,19);export const lastIndex:number=last(values,19);export const laterIndex:number=after(values,19);export const joined:string=label(values);export const sliced:number[]=middle(values);export const writable:number[]=copy(values);writable[0]=99;export const original:number=values[0];export const appended:number[]=append(values,[23,24]);appended[0]=42;export const end:number|undefined=atEnd(values);export const missing:number|undefined=outOfRange(values);export const reversed:number[]=reversedCopy(values);export const sorted:number[]=sortedCopy(values);export const replaced:number[]=withCopy(values);export const spliced:number[]=splicedCopy(values);export const trimmed:number[]=trimmedCopy(values);'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const built=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {found,firstIndex,lastIndex,laterIndex,joined,sliced,writable,original,appended,end,missing,reversed,sorted,replaced,spliced,trimmed} from "./entry.js";import {values} from "./model.js";if(replaced===values||spliced===values||trimmed===values||replaced.join(",")!=="19,20,21,99"||spliced.join(",")!=="19,41,42,19"||trimmed.join(",")!=="19,20"||values.join(",")!=="19,20,21,19"||reversed===values||sorted===values||reversed.join(",")!=="19,21,20,19"||sorted.join(",")!=="19,19,20,21"||found!==true||firstIndex!==0||lastIndex!==3||laterIndex!==3||joined!=="19/20/21/19"||sliced.length!==2||sliced[0]!==20||sliced[1]!==21||writable[0]!==99||original!==19||appended.length!==7||appended[0]!==42||appended[4]!==23||appended[5]!==24||appended[6]!==22||end!==19||missing!==undefined)throw Error(JSON.stringify({found,firstIndex,lastIndex,laterIndex,joined,sliced,writable,original,appended,end,missing}));'],
    {cwd:folder,encoding:'utf8',timeout:15_000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const rejected:[string,string,string][]=[
    ['wrong-includes','let xs:number[]=[21];xs.includes("bad");','TS2345'],
    ['missing-includes','let xs:number[]=[21];xs.includes();','TS2554'],
    ['wrong-start','let xs:number[]=[21];xs.includes(21,"bad");','TS2345'],
    ['extra-includes','let xs:number[]=[21];xs.includes(21,0,2);','TS2554'],
    ['missing-index','let xs:number[]=[21];xs.indexOf();','TS2554'],
    ['wrong-last','let xs:readonly number[]=[21];xs.lastIndexOf("bad");','TS2345'],
    ['wrong-separator','let xs:readonly number[]=[21];xs.join(42);','TS2345'],
    ['extra-join','let xs:number[]=[21];xs.join(",",",");','TS2554'],
    ['bad-result','let xs:number[]=[21];let b:boolean=xs.indexOf(21);','TS2322'],
    ['readonly-mutation','let xs:ReadonlyArray<number>=[21];xs.push(22);','TS2339'],
    ['bad-slice-start','let xs:number[]=[21];xs.slice("bad");','TS2345'],
    ['bad-slice-end','let xs:readonly number[]=[21];xs.slice(0,"bad");','TS2345'],
    ['bad-slice-arity','let xs:number[]=[21];xs.slice(0,1,2);','TS2554'],
    ['bad-slice-result','let xs:ReadonlyArray<number>=[21];let n:number=xs.slice(0);','TS2322'],
    ['bad-slice-element','let xs:readonly string[]=["a"];let n:number[]=xs.slice();','TS2322'],
    ['bad-concat-1',"let xs:number[]=[1];xs.concat(\"bad\");",'TS2345'],
    ['bad-concat-2',"let xs:readonly number[]=[1];let wrong:string[]=[\"bad\"];xs.concat(wrong);",'TS2345'],
    ['bad-concat-3',"let xs:number[]=[1];let wrong:string[]=xs.concat(2);",'TS2322'],
    ['bad-concat-4',"let xs:readonly string[]=[\"a\"];xs.concat(42);",'TS2345'],
    ['bad-concat-5',"let xs:number[]=[1];let extra:string[]=[\"bad\"];xs.concat(...extra);",'TS2345'],
    ['bad-concat-6',"let xs:ReadonlyArray<number>=[1];let wrong:readonly string[]=xs.concat([2]);",'TS2322'],
    ['bad-concat-7',"let xs:number[]=[1];xs.concat(true);",'TS2345'],
    ['bad-at-missing','let xs:number[]=[1];xs.at();','TS2554'],
    ['bad-at-string','let xs:number[]=[1];xs.at("0");','TS2345'],
    ['bad-at-boolean','let xs:readonly number[]=[1];xs.at(true);','TS2345'],
    ['bad-at-extra','let xs:readonly number[]=[1];xs.at(0,1);','TS2554'],
    ['bad-at-return','let xs:number[]=[1];let wrong:string=xs.at(0);','TS2322'],
    ['bad-at-readonly-return','let xs:ReadonlyArray<number>=[1];let wrong:boolean=xs.at(-1);','TS2322'],
    ['bad-at-callback','let xs:number[]=[1];xs.at((n:number):number=>n);','TS2345'],
    ['bad-at-string-return','let xs:readonly string[]=["a"];let wrong:number|undefined=xs.at(0);','TS2322'],
    ['copy-reverse-arity','let xs:number[]=[1];xs.toReversed(0);','TS2554'],
    ['copy-sort-type','let xs:readonly number[]=[1];xs.toSorted(1);','TS2345'],
    ['copy-sort-return','let xs:number[]=[1];xs.toSorted((a:number,b:number):boolean=>true);','TS2345'],
    ['copy-reverse-assignment','let xs:readonly string[]=["a"];let out:number[]=xs.toReversed();','TS2322'],
    ['copy-sort-assignment','let xs:number[]=[1];let out:string[]=xs.toSorted();','TS2322'],
    ['copy-sort-comparator','let xs:ReadonlyArray<number>=[1];xs.toSorted((a:string,b:string):number=>0);','TS2345'],
    ['copy-sort-extra','let xs:number[]=[1];xs.toSorted(undefined,undefined);','TS2554'],
    ['copy-reverse-extra','let xs:number[]=[1];xs.toReversed("bad");','TS2554'],
    ['with-arity','let xs:number[]=[1];xs.with(0);','TS2554'],
    ['with-index','let xs:number[]=[1];xs.with("bad",2);','TS2345'],
    ['with-value','let xs:number[]=[1];xs.with(0,"bad");','TS2345'],
    ['with-assignment','let xs:readonly string[]=["a"];let out:number[]=xs.with(0,"b");','TS2322'],
    ['spliced-arity','let xs:number[]=[1];xs.toSpliced();','TS2554'],
    ['spliced-start','let xs:number[]=[1];xs.toSpliced("bad",0);','TS2345'],
    ['spliced-item','let xs:number[]=[1];xs.toSpliced(0,1,"bad");','TS2345'],
    ['spliced-assignment','let xs:number[]=[1];let out:string[]=xs.toSpliced(0,1);','TS2322'],
    ['spliced-undefined-insertion','let xs:number[]=[1,2];xs.toSpliced(0,undefined,3);','TS2345']
  ];
  for(const [name,source,expected] of rejected) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(expected),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('array queries: four linked ES2015 modules executed; forty-seven invalid programs blocked output.');
} finally {rmSync(folder,{recursive:true,force:true});}
