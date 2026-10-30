import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module actual ES2015 execution, NOT original upstream TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-void-unary-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export type Reading={value:number};',
    'logic.ts':[
      'import type {Reading} from "./model.js";',
      'export let calls:number=0;',
      'export function effect(n:number):number {calls+=1;return n;}',
      'export function ignore(r:Reading):undefined{return void effect(r.value);}',
      'export function nested():undefined{return void void effect(1);}'
    ].join('\n'),
    'bridge.ts':'export {effect,ignore,nested,calls} from "./logic.js";',
    'entry.ts':[
      'import {effect,ignore,nested} from "./bridge.js";',
      'export const first:undefined=ignore({value:20});',
      'export const second:undefined=void effect(21);',
      'export const third:undefined=nested();',
      'export const result:number=42;'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(sources))
    writeFileSync(join(dir,name),source+'\n');
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(source:string) {
    const r=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015','--module','es2015',
        source,'minimal.d.ts'],{cwd:dir,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing emitted module '+name);
  assert.match(readFileSync(join(dir,'logic.js'),'utf8'),/\bvoid\b/,
    'unary void must survive in ES2015 output');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {first,second,third,result} from "./entry.js";',
    'import {calls} from "./bridge.js";',
    'if(first!==undefined||second!==undefined||third!==undefined||result!==42||calls!==3)',
    '  throw Error("unary void runtime/single-evaluation regression");'
  ].join('\n')],{cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['number','let wrong:number=void 21;','TS2322',2],
    ['string','let wrong:string=void "ignored";','TS2322',2],
    ['boolean','let wrong:boolean=void true;','TS2322',2],
    ['return','function wrong():number{return void 21;}','TS2322',2],
    ['unresolved','let wrong:undefined=void missing;','TS2304',2],
    ['exponent','let wrong:undefined=void 2 ** 3;','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid) {
    writeFileSync(join(dir,'invalid-'+id+'.ts'),source);
    const r=compile('invalid-'+id+'.ts');
    assert.equal(r.status,exit,r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(code));
    assert.equal(existsSync(join(dir,'invalid-'+id+'.js')),false,'invalid emitted JS: '+id);
  }
  console.log('unary void: four-module Node ES2015 result 42, three effects; six no-emit negatives');
} finally {rmSync(dir,{recursive:true,force:true});}
