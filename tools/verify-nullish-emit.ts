import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module ES2015 output and negative no-emit, not original TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-nullish-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Option { value?: number; run?(n:number):number }',
    'data.ts':[
      'import type {Option} from "./model.js";',
      'export function create(present:boolean):Option {return present ? {value:21} : {}; }',
      'export let hits:number=0;',
      'export function fallback():number {hits+=1;return 99;}',
      'export let reads:number=0;',
      'export function readOnce():number|undefined {reads+=1;return undefined;}'
    ].join('\n'),
    'logic.ts':[
      'import type {Option} from "./model.js";',
      'import {fallback} from "./data.js";',
      'export function choose(o:Option):number {return o.value ?? fallback();}',
      'export function callback(o:Option):number {',
      '  const f:(n:number)=>number=o.run ?? ((n:number):number=>n+n);',
      '  return f(21);',
      '}'
    ].join('\n'),
    'entry.ts':[
      'import {create,hits,reads,readOnce} from "./data.js";',
      'import {choose,callback} from "./logic.js";',
      'export const result:number=choose(create(true))*2;',
      'export const absent:number=choose(create(false));',
      'export const callbackResult:number=callback(create(false));',
      'export const once:number=readOnce() ?? 42;',
      'export const hitCount:number=hits;',
      'export const readCount:number=reads;'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(sources))writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const run=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(run.error,undefined,String(run.error));
    assert.equal(run.signal,null,'compiler crashed or timed out');
    return run;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','data','logic','entry']) {
    const out=join(dir,name+'.js');
    assert.ok(existsSync(out),'missing emitted '+name);
    assert.doesNotMatch(readFileSync(out,'utf8'),/\?\?/,'ES2015 output must lower ??');
  }
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,absent,callbackResult,once,hitCount,readCount} from "./entry.js"; if(result!==42||absent!==99||callbackResult!==42||once!==42||hitCount!==1||readCount!==1)throw Error("nullish runtime/laziness mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['wrong-fallback','let c:{value?:number}={};let n:number=c.value ?? "bad";','TS2322',2],
    ['missing-name','let c:{value?:number}={};let n:number=c.value ?? absent;','TS2304',2],
    ['wrong-type','let c:{value?:number}={};let n:boolean=c.value ?? 42;','TS2322',2],
    ['bad-logical','let c:{value?:number}={};let n:number=c.value ?? 42 || 0;','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid) {
    const path=join(dir,'invalid-'+id+'.ts');
    writeFileSync(path,source);
    const out=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(out.status,exit,out.stdout+out.stderr);
    assert.match(out.stdout+out.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid program emitted '+id);
  }
  console.log('nullish coalescing: four-module ES2015 runtime 42, lazy fallback, one left read, four no-emit errors');
} finally {rmSync(dir,{recursive:true,force:true});}
