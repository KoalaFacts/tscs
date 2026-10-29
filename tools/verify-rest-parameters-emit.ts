import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-rest-parameters-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item={value:number};\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export function sum(base:number,...values:number[]):number{',
    '  let total:number=base;for(const value of values){total+=value;}return total;',
    '}',
    'export function count(...values:number[]):number{return values.length;}',
    'export function read(...items:Item[]):number{',
    '  let total:number=0;for(const item of items){total+=item.value;}return total;',
    '}',
    'export function withDefault(base:number=21,...rest:number[]):number{return base+rest.length;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {sum,count,read,withDefault} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {sum,count,read,withDefault} from "./bridge.js";',
    'export const result:number=sum(40,1,1);',
    'export const empty:number=sum(42);',
    'export const indexed:number=count(20,21,1);',
    'export const nested:number=read({value:20},{value:21},{value:1});',
    'export const defaulted:number=withDefault();',
    'export const extended:number=withDefault(40,1,2);'
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
  const r=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,empty,indexed,nested,defaulted,extended} from "./entry.js";',
    'if(result!==42||empty!==42||indexed!==3||nested!==42||defaulted!==21||extended!==42)',
    '  throw Error("bad rest parameter runtime");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.status,0,r.stdout+r.stderr);
  const invalid=[
    ['wrong-number','function f(...v:number[]):number{return v.length;}f("bad");','TS2345'],
    ['wrong-string','function f(...v:string[]):number{return v.length;}f("ok",21);','TS2345'],
    ['wrong-after-fixed','function f(n:number,...v:number[]):number{return n;}f(1,"bad");','TS2345'],
    ['not-array','function f(...v:number):number{return 1;}','TS2370'],
    ['not-last','function f(...v:number[],n:number):number{return n;}','TS1014'],
    ['optional','function f(...v?:number[]):number{return 1;}','TS1047'],
    ['default','function f(...v:number[]=[]):number{return 1;}','TS1048']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const checked=compile(name+'.ts');
    assert.notEqual(checked.status,0,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted');
  }
  console.log('Rest params: four-module result=42 empty=42 indexed=3 nested=42 defaulted=21 extended=42; seven no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
