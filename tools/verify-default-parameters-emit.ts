import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-default-parameters-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item={value:number};\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export function add(base:number,extra:number=1):number{return base+extra;}',
    'export function double(base:number,extra:number=base):number{return base+extra;}',
    'export function read(item:Item,extra:number=1):number{return add(item.value,extra);}',
    'export function label(value:string="ok"):string{return value;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {add,double,read,label} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {add,double,read,label} from "./bridge.js";',
    'export const result:number=add(41);',
    'export const supplied:number=add(40,2);',
    'export const prior:number=double(21);',
    'export const nested:number=read({value:41});',
    'export const word:string=label();'
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
    'import {result,supplied,prior,nested,word} from "./entry.js";',
    'if(result!==42||supplied!==42||prior!==42||nested!==42||word!=="ok")throw Error("bad default-parameter runtime");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.status,0,r.stdout+r.stderr);
  const invalid=[
    ['wrong-default','function f(n:number="bad"):number{return n;}','TS2322'],
    ['wrong-bool','function f(n:number=true):number{return n;}','TS2322'],
    ['wrong-call','function f(n:number=1):number{return n;}f("bad");','TS2345'],
    ['extra-call','function f(n:number=1):number{return n;}f(1,2);','TS2554'],
    ['missing','function f(a:number,b:number=1):number{return a+b;}f();','TS2554'],
    ['conflict','function f(n?:number=1):number{return n;}','TS1015'],
    ['nontrailing','function f(n:number=1,x:number):number{return n+x;}','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const checked=compile(name+'.ts');
    assert.notEqual(checked.status,0,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted');
  }
  console.log('Defaults: four-module result=42 supplied=42 prior=42 nested=42 word=ok; seven no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
