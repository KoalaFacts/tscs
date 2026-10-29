import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const dir=mkdtempSync(join(tmpdir(),'tscs-type-query-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(dir,'model.ts'),[
    'export const item:{value:number;nested:{amount:number}}={value:21,nested:{amount:42}};',
    'export type Shape=typeof item;',
    'export type Amount=typeof item.nested.amount;',
    'export type Key=keyof typeof item;',
    'export type Value=(typeof item)["value"];'
  ].join('\n'));
  writeFileSync(join(dir,'logic.ts'),[
    'export function twice(n:number):number{return n*2;}',
    'export type Double=typeof twice;',
    'export function apply(fn:Double,n:number):number{return fn(n);}'
  ].join('\n'));
  writeFileSync(join(dir,'bridge.ts'),'export {twice,apply} from "./logic.js";\n');
  writeFileSync(join(dir,'entry.ts'),[
    'import type {Shape,Amount,Key,Value} from "./model.js";',
    'import {item} from "./model.js";',
    'import {twice,apply} from "./bridge.js";',
    'const copy:Shape={value:item.value,nested:{amount:item.nested.amount}};',
    'export const result:Value=apply(twice,copy.value);',
    'export const nested:Amount=copy.nested.amount;',
    'export const key:Key="nested";',
    'type ImportedShape=typeof item;',
    'type ImportedKey=keyof typeof item;',
    'type ImportedValue=(typeof item)["value"];',
    'type ImportedCallback=typeof twice;',
    'function invoke(fn:ImportedCallback,n:number):number{return fn(n);}',
    'const importedCopy:ImportedShape={value:item.value,nested:{amount:item.nested.amount}};',
    'export const importedResult:ImportedValue=invoke(twice,importedCopy.value);',
    'export const importedKey:ImportedKey="value";'
  ].join('\n'));
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const f of ['model','logic','bridge','entry'])assert.ok(existsSync(join(dir,f+'.js')));
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,nested,key,importedResult,importedKey} from "./entry.js"; if(result!==42||nested!==42||key!=="nested"||importedResult!==42||importedKey!=="value")throw Error("type query runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['missing-root','type A=typeof missing;','TS2304',2],
    ['missing-property','const p:{value:number}={value:1};type V=typeof p.absent;','TS2339',2],
    ['wrong-value','const p:{value:number}={value:1};type V=typeof p.value;let n:V="bad";','TS2322',2],
    ['wrong-function','function f(n:number):number{return n;}type Fn=typeof f;let v:Fn=21;','TS2322',2],
    ['inferred-variable','let raw=21;type R=typeof raw;','TSCS1001',1],
    ['recursive-type','type R=typeof v;let v:R=21;','TS2456',2],
    ['unknown-property','const p:{nested:{amount:number}}={nested:{amount:1}};type V=typeof p.nested.bad;','TS2339',2]
  ];
  for(const [name,code,diagnostic,exit] of invalid) {
    const file=join(dir,'bad-'+name+'.ts');
    writeFileSync(file,code);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'failed source emitted JavaScript');
  }
  // Each imported type query is checked using the same real module graph.
  writeFileSync(join(dir,'untyped.ts'),'export const untyped=21;');
  const importedInvalid:[string,string,string,number][]=[
    ['imported-missing-member','import {item} from "./model.js";type Bad=typeof item.missing;','TS2339',2],
    ['imported-wrong-value','import {item} from "./model.js";type N=(typeof item)["value"];let bad:N="no";','TS2322',2],
    ['imported-function-mismatch','import {twice} from "./logic.js";let bad:typeof twice=42;','TS2322',2],
    ['imported-unsupported-inference','import {untyped} from "./untyped.js";type N=typeof untyped;','TSCS1001',1]
  ];
  for(const [name,source,diagnostic,exit] of importedInvalid) {
    const file=join(dir,'bad-'+name+'.ts');
    writeFileSync(file,source);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',
      '--module','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'failed imported type query emitted JS');
  }
  console.log('typeof type query: imported values/functions across four ES2015 modules, result/nested/importedResult=42; eleven invalid programs rejected.');
} finally {rmSync(dir,{recursive:true,force:true});}
