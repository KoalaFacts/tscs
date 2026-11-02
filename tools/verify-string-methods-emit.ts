import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Real four-module JavaScript execution and no-emit negative diagnostics.
const folder=mkdtempSync(join(tmpdir(),'tscs-string-methods-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(args:string[]){
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  const minimal=readFileSync('fixtures/string-methods/extended-minimal.d.ts','utf8');
  writeFileSync(join(folder,'minimal.d.ts'),minimal);
  const modules={
    'model.ts':'export type Label={text:string};export const sample:Label={text:"  DevWorld  "};',
    'logic.ts':'import type {Label} from "./model.js";export function short(x:Label):string{return x.text.trim().toUpperCase().slice(0,4);}export function match(x:Label):boolean{return x.text.toLowerCase().includes("dev") && x.text.startsWith("  ");}export function suffix(x:Label):boolean{return x.text.trim().endsWith("World");}export function tail(x:Label):string|undefined{return x.text.at(-3);}export function utf16Tail():string|undefined{return "😀".at(-1);}export function repair(s:string):string{return s.toWellFormed();}export function wellFormed(s:string):boolean{return s.isWellFormed();}export function all(s:string):string{return s.replaceAll("a","b");}export function clean(s:string):string{return s.trimLeft().trimRight();}export function upper(s:string):string{return s.toLocaleUpperCase();}export function lower(s:string):string{return s.toLocaleLowerCase();}',
    'bridge.ts':'export {short,match,suffix,tail,utf16Tail,repair,wellFormed,all,clean,upper,lower} from "./logic.js";',
    'entry.ts':'import {sample} from "./model.js";import {short,match,suffix,tail,utf16Tail,repair,wellFormed,all,clean,upper,lower} from "./bridge.js";export const label:string=short(sample);export const found:boolean=match(sample);export const ended:boolean=suffix(sample);export const empty:string="abc".slice();export const last:string|undefined=tail(sample);export const missing:string|undefined=sample.text.at(999);export const lowUnit:string|undefined=utf16Tail();export const malformed:boolean=wellFormed("\\uD800");export const repaired:string=repair("\\uD800");export const repairedGood:boolean=wellFormed(repaired);export const healthy:boolean=wellFormed("ok");export const replaced:string=all("a-a");export const cleaned:string=clean("  x  ");export const localeUpper:string=upper("hello");export const localeLower:string=lower("HELLO");'
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(folder,name),source);
  const result=compile(['--emitJs','--noLib','--module','es2015','--target','es2015','entry.ts','minimal.d.ts']);
  assert.equal(result.status,0,result.stdout+result.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {label,found,ended,empty,last,missing,lowUnit,malformed,repaired,repairedGood,healthy,replaced,cleaned,localeUpper,localeLower} from "./entry.js";if(replaced!=="b-b"||cleaned!=="x"||localeUpper!=="HELLO"||localeLower!=="hello"||malformed!==false||repaired.length!==1||repaired.charCodeAt(0)!==0xfffd||repairedGood!==true||healthy!==true||label!=="DEVW"||found!==true||ended!==true||empty!=="abc"||last!=="d"||missing!==undefined||lowUnit?.charCodeAt(0)!==0xde00)throw Error(JSON.stringify({label,found,ended,empty,last,missing,lowUnit}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const cohort=JSON.parse(readFileSync('fixtures/string-methods/semantic-cases.json','utf8'));
  const invalid=cohort.cases.filter((entry:{id:string})=>entry.id.startsWith('string-methods:negative-'))
    .map((entry:{source:string;expectedCodes:string[]})=>{
      assert.equal(entry.expectedCodes.length,1,'negative case must have one diagnostic');
      return [entry.source,entry.expectedCodes[0]];
    });
  assert.equal(invalid.length,36,'do not silently omit registered negative programs');
  for(let i=0;i<invalid.length;i++){
    const [source,code]=invalid[i],name='invalid-'+i;
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(result.status,2,name+': '+result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code),name);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS: '+name);
  }
  // Malformed, duplicated and unsupported ambient methods may never grant APIs.
  const badAmbient=[
    minimal.replace('  trim(): string;','  trim(): string;\n  trim(): string;'),
    minimal.replace('  trim(): string;','  trim(value: number): string;'),
    minimal.replace('  trim(): string;','  imaginary(): string;'),
    minimal.replace('  at(index: number): string | undefined;','  at(index: string): string | undefined;'),
    minimal.replace('  at(index: number): string | undefined;','  at(index: number): string | undefined;\n  at(index: number): string | undefined;'),
    minimal.replace('  isWellFormed(): boolean;','  isWellFormed(): string;'),
    minimal.replace('  toWellFormed(): string;','  toWellFormed(): string;\n  toWellFormed(): string;'),
    minimal.replace('  replaceAll(searchValue: string, replaceValue: string): string;','  replaceAll(searchValue: number, replaceValue: string): string;'),
    minimal.replace('  replaceAll(searchValue: string, replaceValue: string): string;','  replaceAll(searchValue: string, replaceValue: string): string;\n  replaceAll(searchValue: string, replaceValue: string): string;'),
    minimal.replace('  trimRight(): string;','  trimRight(index: number): string;'),
    minimal.replace('  toLocaleUpperCase(): string;','  toLocaleUpperCase(): boolean;')
  ];
  for(let i=0;i<badAmbient.length;i++){
    const name='ambient-'+i;
    writeFileSync(join(folder,name+'.d.ts'),badAmbient[i]);
    writeFileSync(join(folder,name+'.ts'),'const x:string="hi".trim();');
    const result=compile(['--emitJs','--noLib','--target','es2015',name+'.ts',name+'.d.ts']);
    assert.notEqual(result.status,0,name+' invalid ambient accepted');
    assert.match(result.stdout+result.stderr,/TSCS1002/,name);
    assert.equal(existsSync(join(folder,name+'.js')),false,name+' emitted JS');
  }
  console.log('String native methods: four linked ES2015 modules executed, '+invalid.length+' negative programs and 11 malformed ambient profiles rejected.');
} finally {rmSync(folder,{recursive:true,force:true});}
