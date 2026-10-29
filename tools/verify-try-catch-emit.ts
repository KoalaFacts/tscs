import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual checked cross-module ES2015 output, not original upstream acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-try-catch-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item = {value:number};\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Item} from "./model.js";',
    'export let visits:number=0;',
    'export function recover(n:number):number {',
    '  const item:Item={value:n};',
    '  try {if(n<0){throw item.value;}return item.value+1;}',
    '  catch(error) {visits+=1;return 41;}',
    '  finally {visits+=10;}',
    '}',
    'export function optional(n:number):number {',
    '  try {if(n<0){throw n;}return n;}catch{return 42;}',
    '}',
    'export function override(n:number):number {',
    '  try {throw n;}catch(error){return 21;}finally{return 42;}',
    '}',
    'export function rethrow(n:number):number {',
    '  try {throw n;}catch(error){throw error;}finally{visits+=1;}',
    '}',
    'export function nested(n:number):number {',
    '  try {try{throw n;}catch(inner){throw inner;}}catch(outer){return 42;}',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),
    'export {recover,optional,override,rethrow,nested,visits} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {recover,optional,override,nested} from "./bridge.js";',
    'export const result:number=recover(41);',
    'export const recovered:number=recover(-1);',
    'export const withoutBinding:number=optional(-1);',
    'export const overridden:number=override(-7);',
    'export const nestedResult:number=nested(42);'
  ].join('\n'));
  function compile(source:string) {
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing generated '+name);
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,recovered,withoutBinding,overridden,nestedResult} from "./entry.js";',
    'import {rethrow,visits} from "./bridge.js";',
    'if(result!==42||recovered!==41||withoutBinding!==42||overridden!==42||nestedResult!==42)',
    '  throw Error("try/catch value mismatch");',
    'if(visits!==21)throw Error("incorrect cleanup/catch count");',
    'let caught=0;',
    'try{rethrow(-7);}catch(error){if(error!==-7)throw error;caught++;}',
    'if(caught!==1||visits!==22)throw Error("rethrow or finally mismatch");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid=[
    ['missing-handler','function f():number {try{return 42;}}','TSCS1001'],
    ['missing-catch-name','try{throw 1;}catch(e){let n:number=missing;}','TS2304'],
    ['catch-out-of-scope','try{throw 1;}catch(error){let n:number=error;}let n:number=error;','TS2304'],
    ['catch-redeclaration','try{throw 1;}catch(error){let error:number=21;}','TSCS1001'],
    ['bad-return','function f():number {try{throw 21;}catch(e){return "wrong";}}','TS2322'],
    ['typed-binding','try{throw 1;}catch(error:number){let n:number=21;}','TSCS1001'],
    ['unknown-assignment','try{throw 21;}catch(error){let n:number=error;}','TS2322'],
    ['unknown-member','try{throw {code:21};}catch(error){let n:number=error.code;}','TS18046']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(name+'.ts');
    assert.notEqual(result.status,0,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid source emitted JavaScript');
  }
  console.log('Try/catch: four-module ES2015 result=42, recovered=41, optional=42; finally/rethrow verified; 8 no-emit negatives.');
} finally {rmSync(folder,{recursive:true,force:true});}
