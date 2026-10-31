import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Executes actual generated ES2015 across four modules, not original TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-delete-optional-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Mutable {value?:number;run?(n:number):number;label:string}',
    'data.ts':'import type {Mutable} from "./model.js";export const present:Mutable={value:21,run:(n:number)=>n+n,label:"ready"};export const absent:Mutable={label:"empty"};export const nullablePresent:Mutable={value:13,run:(n:number)=>n+1,label:"nullable"};export const nullish:Mutable|null=null;export const nested:{inner:Mutable}={inner:{value:31,label:"nested"}};export const nullableNested:{inner:Mutable|null}={inner:{value:36,label:"nullable nested"}};export const nullNested:{inner:Mutable|null}={inner:null};export const indexed:Mutable[]=[{value:44,label:"indexed"}];export const factoryTarget:Mutable={value:52,label:"factory"};export const nullableFactoryTarget:Mutable={value:64,label:"maybe factory"};export let factoryCalls:number=0;export function getTarget():Mutable{factoryCalls+=1;return factoryTarget;}export function getMaybe():Mutable|null{factoryCalls+=1;return nullableFactoryTarget;}export const outerPresent:{inner:Mutable}={inner:{value:73,label:"outer"}};export const outerNull:{inner:Mutable}|null=null;export const outerMaybe:{inner:Mutable}|null={inner:{value:84,label:"maybe outer"}};export const outerArray:{items:Mutable[]}|null={items:[{value:97,label:"outer array"}]};export let outerFactoryCalls:number=0;export function getOuter():{inner:Mutable}|null{outerFactoryCalls+=1;return outerMaybe;}',
    'logic.ts':'import {getTarget,getMaybe,getOuter} from "./data.js";import type {Mutable} from "./model.js";export function clearValue(x:Mutable):boolean{return delete x.value;}export function clearRun(x:Mutable):boolean{return delete x["run"]; }export function clearMaybe(x:Mutable|null|undefined):boolean{return delete x?.value;}export function clearMaybeRun(x:Mutable|null|undefined):boolean{return delete x?.["run"];}export function clearGrouped(x:Mutable|null):boolean{return delete (x?.value);}export function clearNested(h:{inner:Mutable}):boolean{return delete h.inner.value;}export function clearNullableNested(h:{inner:Mutable|null}):boolean{return delete h.inner?.value;}export function clearIndexed(xs:Mutable[]):boolean{return delete xs[0]["value"];}export function clearFromFactory():boolean{return delete getTarget().value;}export function clearFromNullableFactory():boolean{return delete getMaybe()?.["value"];}export function clearOuter(h:{inner:Mutable}|null):boolean{return delete h?.inner.value;}export function clearOuterByKey(h:{inner:Mutable}|null):boolean{return delete h?.inner["value"];}export function clearOuterIndexed(h:{items:Mutable[]}|null):boolean{return delete h?.items[0].value;}export function clearOuterFromFactory():boolean{return delete getOuter()?.inner.value;}',
    'entry.ts':'import {present,absent,nullablePresent,nullish,nested,nullableNested,nullNested,indexed,factoryCalls,outerPresent,outerNull,outerMaybe,outerArray,outerFactoryCalls} from "./data.js";import {clearNested,clearNullableNested,clearIndexed,clearFromFactory,clearFromNullableFactory,clearOuter,clearOuterByKey,clearOuterIndexed,clearOuterFromFactory} from "./logic.js";import {clearValue,clearRun,clearMaybe,clearMaybeRun,clearGrouped} from "./logic.js";export const before:number|undefined=present.value;export const first:boolean=clearValue(present);export const second:boolean=clearValue(absent);export const third:boolean=clearRun(present);export const after:number|undefined=present.value;export const afterCall:number|undefined=present.run?.(21);export const nullGuard:boolean=clearMaybe(nullish);export const undefinedGuard:boolean=clearMaybe(undefined);export const removedValue:boolean=clearMaybe(nullablePresent);export const removedMethod:boolean=clearMaybeRun(nullablePresent);export const groupedGuard:boolean=clearGrouped(nullish);export const nestedDeleted:boolean=clearNested(nested);export const nullableNestedDeleted:boolean=clearNullableNested(nullableNested);export const nullNestedDeleted:boolean=clearNullableNested(nullNested);export const indexDeleted:boolean=clearIndexed(indexed);export const factoryDeleted:boolean=clearFromFactory();export const nullableFactoryDeleted:boolean=clearFromNullableFactory();export const factoryCount:number=factoryCalls;export const outerDeleted:boolean=clearOuter(outerPresent);export const outerGuard:boolean=clearOuter(outerNull);export const outerKeyDeleted:boolean=clearOuterByKey(outerMaybe);export const outerIndexedDeleted:boolean=clearOuterIndexed(outerArray);export const outerFactoryDeleted:boolean=clearOuterFromFactory();export const outerFactoryCount:number=outerFactoryCalls;'
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function compile(args:string[]){
    const result=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{
      cwd:dir,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
      env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler timed out or crashed');
    return result;
  }
  const built=compile(['--emitJs','-p','project.json']);
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','data','logic','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing emitted JavaScript: '+name);
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {before,first,second,third,after,afterCall,nullGuard,undefinedGuard,removedValue,removedMethod,groupedGuard,nestedDeleted,nullableNestedDeleted,nullNestedDeleted,indexDeleted,factoryDeleted,nullableFactoryDeleted,factoryCount,outerDeleted,outerGuard,outerKeyDeleted,outerIndexedDeleted,outerFactoryDeleted,outerFactoryCount} from "./entry.js";'+
    'import {present,absent,nullablePresent,nested,nullableNested,indexed,factoryTarget,nullableFactoryTarget,outerPresent,outerMaybe,outerArray} from "./data.js";'+
    'if(before!==21||first!==true||second!==true||third!==true||after!==undefined||afterCall!==undefined||"value" in present||"run" in present||"value" in absent||nullGuard!==true||undefinedGuard!==true||removedValue!==true||removedMethod!==true||groupedGuard!==true||"value" in nullablePresent||"run" in nullablePresent||nestedDeleted!==true||nullableNestedDeleted!==true||nullNestedDeleted!==true||indexDeleted!==true||factoryDeleted!==true||nullableFactoryDeleted!==true||factoryCount!==2||"value" in nested.inner||"value" in nullableNested.inner||"value" in indexed[0]||"value" in factoryTarget||"value" in nullableFactoryTarget||outerDeleted!==true||outerGuard!==true||outerKeyDeleted!==true||outerIndexedDeleted!==true||outerFactoryDeleted!==true||outerFactoryCount!==1||"value" in outerPresent.inner||"value" in outerMaybe.inner||"value" in outerArray.items[0])throw Error("optional delete runtime mismatch");'
  ],{cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);
  const invalid:[string,string,string,number][]=[
    ['required','let x:{value:number}={value:1};let ok:boolean=delete x.value;','TS2790',2],
    ['readonly','let x:{readonly value?:number}={value:1};let ok:boolean=delete x.value;','TS2704',2],
    ['bare','let x:{value?:number}={value:1};let ok:boolean=delete x;','TSCS1001',1],
    ['literal','let ok:boolean=delete 21;','TSCS1001',1],
    ['required-chain','function f(x:{value:number}|null):boolean{return delete x?.value;}','TS2790',2],
    ['readonly-chain','function f(x:{readonly value?:number}|null):boolean{return delete x?.value;}','TS2704',2],
    ['dynamic-chain','function f(x:{value?:number}|null,k:string):boolean{return delete x?.[k];}','TSCS1001',1],
    ['array-chain','function f(x:number[]|null):boolean{return delete x?.[0];}','TSCS1001',1],
    ['nested-required','let h:{inner:{value:number}}={inner:{value:1}};let ok:boolean=delete h.inner.value;','TS2790',2],
    ['nested-readonly','let h:{inner:{readonly value?:number}}={inner:{value:1}};let ok:boolean=delete h.inner.value;','TS2704',2],
    ['nested-dynamic','let h:{inner:{value?:number}}={inner:{value:1}};let k:string="value";let ok:boolean=delete h.inner[k];','TSCS1001',1],
    ['continued-required','function f(h:{inner:{value:number}}|null):boolean{return delete h?.inner.value;}','TS2790',2],
    ['continued-readonly','function f(h:{inner:{readonly value?:number}}|null):boolean{return delete h?.inner.value;}','TS2704',2],
    ['continued-dynamic','function f(h:{inner:{value?:number}}|null,k:string):boolean{return delete h?.inner[k];}','TSCS1001',1],
    ['continued-array','function f(h:{items:number[]}|null):boolean{return delete h?.items[0];}','TSCS1001',1],
    ['array','let x:number[]=[1,2];let ok:boolean=delete x[0];','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'invalid-'+id+'.ts');writeFileSync(path,source);
    const result=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(result.status,exit,result.stdout+'\n'+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid emitted JavaScript: '+id);
  }
  console.log('delete optional: 4 linked ES2015 modules executed; 16 invalid cases blocked emission');
} finally {rmSync(dir,{recursive:true,force:true});}
