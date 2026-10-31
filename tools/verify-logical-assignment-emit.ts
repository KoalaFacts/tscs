import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four linked ES2015 modules and genuine no-emit negatives; NOT original TS7.
const dir=mkdtempSync(join(tmpdir(),'tscs-logical-assignment-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export type Maybe = number | null;',
    'data.ts':'export let numberCalls:number=0;export let boolCalls:number=0;export let receiverCalls:number=0;export let indexCalls:number=0;export function fallback():number{numberCalls+=1;return 42;}export function toggle():boolean{boolCalls+=1;return false;}export function createBox():{value?:number}{receiverCalls+=1;return {}}export function nextIndex():number{indexCalls+=1;return 0;}',
    'logic.ts':'import type {Maybe} from "./model.js";import {fallback,toggle,createBox,nextIndex} from "./data.js";export function choose(n:Maybe):number{n??=fallback();return n!;}export function andCase(v:boolean):boolean{v&&=toggle();return v;}export function orCase(v:boolean):boolean{v||=toggle();return v;}export function fillBox():number{const b:{value?:number}={};b.value??=fallback();b.value??=fallback();return b.value!;}export function fromFactory():number{const b:{value?:number}=createBox();b["value"]??=fallback();return b.value!;}export function indexCase():number{let a:number[]=[21];a[nextIndex()]??=fallback();return a[0];}export function flags():boolean{const b:{flag:boolean}={flag:true};b.flag&&=toggle();b.flag||=true;return b.flag;}export function effectReceiver():number{createBox().value??=fallback();return 0;}export function primitiveTruthiness():number{let n:number=0;n&&=fallback();n||=fallback();n||=fallback();n&&=fallback();return n;}export function textTruthiness():string{let s:string="";s||="ready";s&&="updated";s||="wrong";return s;}export function memberTruthiness():number{const b:{value?:number}={};b.value||=21;b.value&&=42;return b.value!;}export function arrayTruthiness():number{let a:number[]=[0];a[0]||=21;a[0]&&=42;return a[0];}export function objectTruthiness():number{let b:{value:number}={value:1};b||={value:2};b&&={value:3};return b.value;}export function stringIndexTruthiness():string{let b:{name:string}={name:""};b["name"]||="ready";b.name&&="done";return b.name;}export function nullableTruthiness():number{let n:number|null=null;n||=21;return n!;}',
    'entry.ts':'import {choose,andCase,orCase,fillBox,fromFactory,indexCase,flags,effectReceiver,primitiveTruthiness,textTruthiness,memberTruthiness,arrayTruthiness,objectTruthiness,stringIndexTruthiness,nullableTruthiness} from "./logic.js";import {numberCalls,boolCalls,receiverCalls,indexCalls} from "./data.js";export const missing:number=choose(null);export const present:number=choose(21);export const andTrue:boolean=andCase(true);export const andFalse:boolean=andCase(false);export const orFalse:boolean=orCase(false);export const orTrue:boolean=orCase(true);export const filled:number=fillBox();export const factoryFilled:number=fromFactory();export const indexed:number=indexCase();export const flagged:boolean=flags();export const effect:number=effectReceiver();export const numberCount:number=numberCalls;export const boolCount:number=boolCalls;export const receiverCount:number=receiverCalls;export const keyCount:number=indexCalls;export const truthyNumber:number=primitiveTruthiness();export const truthyText:string=textTruthiness();export const truthyMember:number=memberTruthiness();export const truthyArray:number=arrayTruthiness();export const truthyObject:number=objectTruthiness();export const truthyIndexedString:string=stringIndexTruthiness();export const truthyNullable:number=nullableTruthiness();'
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function compile(args:string[]){
    const r=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{cwd:dir,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}});
    assert.equal(r.error,undefined,String(r.error));assert.equal(r.signal,null);return r;
  }
  const built=compile(['--emitJs','-p','project.json']);
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','data','logic','entry']){
    const file=join(dir,name+'.js');assert.ok(existsSync(file),'missing output '+name);
    assert.doesNotMatch(readFileSync(file,'utf8'),/&&=|\|\|=|\?\?=/,'logical assignments must lower to ES2015');
  }
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {missing,present,andTrue,andFalse,orFalse,orTrue,filled,factoryFilled,indexed,flagged,effect,numberCount,boolCount,receiverCount,keyCount,truthyNumber,truthyText,truthyMember,truthyArray,truthyObject,truthyIndexedString,truthyNullable} from "./entry.js";if(missing!==42||present!==21||andTrue!==false||andFalse!==false||orFalse!==false||orTrue!==true||filled!==42||factoryFilled!==42||indexed!==21||flagged!==true||effect!==0||numberCount!==6||boolCount!==3||receiverCount!==2||keyCount!==1||truthyNumber!==42||truthyText!=="updated"||truthyMember!==42||truthyArray!==42||truthyObject!==3||truthyIndexedString!=="done"||truthyNullable!==21)throw Error("logical assignment runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));assert.equal(run.status,0,run.stdout+'\n'+run.stderr);
  const invalid:[string,string,string,number][]=[
    ['wrong-nullish','let n:number|null=null;n??="bad";','TS2322',2],
    ['const','const n:number|null=null;n??=21;','TS2588',2],
    ['wrong-and','let b:boolean=true;b&&=21;','TS2322',2],
    ['wrong-or','let b:boolean=false;b||="bad";','TS2322',2],
    ['wrong-member-truthiness','let b:{value?:number}={};b.value&&=false;','TS2322',2],
    ['uninitialised','let n:number;n??=42;','TSCS1001',1],
    ['readonly-member','let b:{readonly value?:number}={};b.value??=42;','TS2540',2],
    ['wrong-member-type','let b:{value?:number}={};b.value??="bad";','TS2322',2],
    ['optional-target','let b:{value?:number}={};b?.value??=42;','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid) {
    const input=join(dir,'invalid-'+id+'.ts');writeFileSync(input,source);
    const result=compile(['--emitJs','--noLib','--target','es2015',input,join(dir,'minimal.d.ts')]);
    assert.equal(result.status,exit,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(input.replace(/\.ts$/,'.js')),false,'invalid code emitted: '+id);
  }
  console.log('logical assignment: 4 linked ES2015 modules executed, numeric/string/object truthiness, once-only member/index and lazy RHS; 9 negative programs blocked');
} finally {rmSync(dir,{recursive:true,force:true});}
