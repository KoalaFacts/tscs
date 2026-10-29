import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-optional-parameters-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item={value:number};\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export function sum(base:number,extra?:number):number {',
    '  if(typeof extra==="number"){return base+extra;}return base;',
    '}',
    'export function read(item:Item,extra?:number):number{return sum(item.value,extra);}',
    'export function pick(a?:number,b?:number):number {',
    '  if(typeof a==="number"){if(typeof b==="number"){return a+b;}return a;}return 0;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {sum,read,pick} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {sum,read,pick} from "./bridge.js";',
    'export const result:number=sum(41,1);',
    'export const omitted:number=sum(21);',
    'export const nested:number=read({value:20},1);',
    'export const blank:number=pick();'
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
    'import {result,omitted,nested,blank} from "./entry.js";',
    'if(result!==42||omitted!==21||nested!==21||blank!==0)throw Error("bad optional call runtime");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.status,0,r.stdout+r.stderr);
  const invalid=[
    ['missing','function f(a:number,b?:number):number{return a;}f();','TS2554'],
    ['excess','function f(a:number,b?:number):number{return a;}f(1,2,3);','TS2554'],
    ['wrong-optional','function f(a?:number):void{return;}f("bad");','TS2345'],
    ['wrong-required','function f(a:number,b?:string):number{return a;}f("bad");','TS2345'],
    ['ordering','function f(a?:number,b:number):number{return b;}','TS1016']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const checked=compile(name+'.ts');
    assert.notEqual(checked.status,0,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted');
  }
  console.log('Optional params: four-module result=42 omitted=21 nested=21 blank=0; five no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
