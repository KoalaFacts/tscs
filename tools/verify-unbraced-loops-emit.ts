import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four linked ES2015 modules executed in Node, not upstream acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-unbraced-loops-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Values = number[]; export interface RecordData { a:number; b:number }');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Values, RecordData } from "./model.js";',
    'export function countdown(n:number):number { while(n>0) n--; return n; }',
    'export function atLeastOnce(n:number):number { let i:number=0; do i++; while(i<n); return i; }',
    'export function sumFor(n:number):number { let total:number=0; for(let i=0;i<n;i++) total+=i; return total; }',
    'export function sumOf(values:Values):number { let total:number=0; for(const v of values) if(v<0) continue; else total+=v; return total; }',
    'export function countKeys(data:RecordData):number { let total:number=0; for(const key in data) total++; return total; }',
    'export function nested(n:number):number { while(n>0) if(n===2) break; else n--; return n; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),
    'export { countdown, atLeastOnce, sumFor, sumOf, countKeys, nested } from "./logic.js";');
  writeFileSync(join(folder,'entry.ts'),[
    'import { countdown, atLeastOnce, sumFor, sumOf, countKeys, nested } from "./bridge.js";',
    'export const results:number[]=[countdown(4),atLeastOnce(0),sumFor(4),sumOf([1,-4,2]),countKeys({a:1,b:2}),nested(5)];'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{noLib:true,target:'es2015',module:'es2015'}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const result=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{
      cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env
    });
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const compiled=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(compiled.status,0,compiled.stdout+'\n'+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),name+' did not emit JS');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {results} from "./entry.js"; if(JSON.stringify(results)!=="[0,1,6,3,2,2]") throw Error("unbraced loop runtime mismatch: "+JSON.stringify(results));'
  ],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+'\n'+run.stderr);
  for(const [name,source,code,exit] of [
    ['bad-while','function f(n:number):number{while(n>0)n="bad";return n;}','TS2322',2],
    ['bad-do','function f(n:number):number{do n="bad";while(n>0);return n;}','TS2322',2],
    ['bad-for','function f(n:number):number{for(let i=0;i<n;i++)n="bad";return n;}','TS2322',2],
    ['bad-of','function f(values:number[]):number{let n:number=0;for(const x of values)n="bad";return n;}','TS2322',2],
    ['bad-in','function f(data:{a:number}):number{let n:number=0;for(const key in data)n="bad";return n;}','TS2322',2],
    ['bad-lexical','function f(n:number):number{while(n>0)const x=1;return n;}','TSCS1001',1]
  ] as const) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(rejected.status,exit,name+': '+rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,name+' emitted invalid JavaScript');
  }
  console.log('Four-module unbraced while/do/for/of/in runtime [0,1,6,3,2,2], six negative no-JS checks passed.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
