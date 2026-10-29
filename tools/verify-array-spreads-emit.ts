import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-array-spreads-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/spread-arguments/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export type Item={value:number};',
    'export const nums:number[]=[20];',
    'export const tail:number[]=[21];',
    'export const empty:number[]=[];',
    'export const items:Item[]=[{value:20}];',
    'export const extra:Item[]=[{value:22}];'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export function sum(values:number[]):number{',
    '  let result:number=0;for(const n of values){result+=n;}return result;',
    '}',
    'export function read(items:Item[]):number{',
    '  let result:number=0;for(const item of items){result+=item.value;}return result;',
    '}',
    'export function size(values:number[]):number{return values.length;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {sum,read,size} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {nums,tail,empty,items,extra} from "./model.js";',
    'import {sum,read,size} from "./bridge.js";',
    'export const result:number=sum([20,...tail,1]);',
    'export const mixed:number=sum([...nums,1,...tail]);',
    'export const nested:number=read([...items,...extra]);',
    'export const emptyResult:number=sum([...empty,42]);',
    'export const pair:number=sum([20,...[21,1]]);',
    'export const length:number=size([1,...nums,...tail]);'
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
    'import {result,mixed,nested,emptyResult,pair,length} from "./entry.js";',
    'if(result!==42||mixed!==42||nested!==42||emptyResult!==42||pair!==42||length!==3)',
    '  throw Error("array spread runtime mismatch");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid=[
    ['nonarray','let xs:number[]=[...42];','TSCS1001'],
    ['boolean','let xs:number[]=[...true];','TSCS1001'],
    ['object','let xs:number[]=[...{value:21}];','TSCS1001'],
    ['string-iterable','let xs:number[]=[..."bad"];','TSCS1001'],
    ['string-elements','let xs:number[]=[...["bad"]];','TS2322'],
    ['incompatible','let xs:string[]=["bad"];let ys:number[]=[...xs];','TS2322'],
    ['missing','let xs:number[]=[...unknown];','TS2304'],
    ['hole','let xs:number[]=[,...[1]];','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const checked=compile(name+'.ts');
    assert.notEqual(checked.status,0,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid spread emitted');
  }
  console.log('Array spreads: four-module runtime result/mixed/nested/empty/pair=42, length=3; eight invalid no-emit programs.');
} finally {rmSync(folder,{recursive:true,force:true});}
