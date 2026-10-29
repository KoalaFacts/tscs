import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-spread-arguments-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/spread-arguments/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export type Item={value:number};',
    'export const nums:number[]=[20,21,1];',
    'export const items:Item[]=[{value:20},{value:21},{value:1}];',
    'export const empty:number[]=[];',
    'export const words:string[]=["a","b"];'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export function sum(base:number,...values:number[]):number{',
    '  let total:number=base;for(const value of values){total+=value;}return total;',
    '}',
    'export function count(...values:string[]):number{return values.length;}',
    'export function read(...items:Item[]):number{',
    '  let total:number=0;for(const item of items){total+=item.value;}return total;',
    '}',
    'export function sized(...values:number[]):number{return values.length;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {sum,count,read,sized} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {nums,items,empty,words} from "./model.js";',
    'import {sum,count,read,sized} from "./bridge.js";',
    'export const result:number=sum(0,...nums);',
    'export const mixed:number=sum(40,1,...[1]);',
    'export const nested:number=read(...items);',
    'export const emptyResult:number=sum(42,...empty);',
    'export const wordCount:number=count("seed",...words);',
    'export const length:number=sized(...[20,21,1]);'
  ].join('\n'));
  function compile(source:string){
    const r=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,mixed,nested,emptyResult,wordCount,length} from "./entry.js";',
    'if(result!==42||mixed!==42||nested!==42||emptyResult!==42||wordCount!==3||length!==3)',
    '  throw Error("spread call produced incorrect ES2015 output");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid=[
    ['wrong-element','function f(...n:number[]):number{return n.length;}let xs:string[]=["bad"];f(...xs);','TS2345'],
    ['wrong-fixed','function f(n:number,...v:number[]):number{return n;}let xs:string[]=["bad"];f(1,...xs);','TS2345'],
    ['wrong-bool','function f(...n:number[]):number{return n.length;}let xs:boolean[]=[true];f(...xs);','TS2345'],
    ['no-rest','function f(n:number):number{return n;}let xs:number[]=[1];f(...xs);','TS2556'],
    ['nonarray','function f(...n:number[]):number{return n.length;}f(...21);','TSCS1001'],
    ['missing-fixed','function f(n:number,...v:number[]):number{return n;}let xs:number[]=[1];f(...xs);','TSCS1001'],
    ['trailing-value','function f(...n:number[]):number{return n.length;}let xs:number[]=[1];f(...xs,1);','TSCS1001'],
    ['two-spreads','function f(...n:number[]):number{return n.length;}let xs:number[]=[1];f(...xs,...xs);','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const checked=compile(name+'.ts');
    assert.notEqual(checked.status,0,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid spread emitted');
  }
  console.log('Spread calls: four-module result/mixed/nested/empty=42, counts=3; eight no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
