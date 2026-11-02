import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module ES2015 execution, separate from bounded reference tests.
const folder=mkdtempSync(join(tmpdir(),'tscs-union-writes-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    "export type Choice={tag:'a';n:number}|{tag:'b';n:number};",
    "export const one:Choice={tag:'a',n:20};",
    "export const two:Choice={tag:'b',n:21};"
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Choice} from "./model.js";',
    'export function bump(x:Choice):number {x.n+=1;x["n"]+=1;x.n++;x["n"]++;return x.n;}',
    'export function set(x:Choice,value:number):number {x.n=value;return x.n;}',
    'export function fallback(x:Choice):number {x.n ||= 42;return x.n;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {bump,set,fallback} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {one,two} from "./model.js";',
    'import {bump,set,fallback} from "./bridge.js";',
    'export const resultA:number=bump(one);',
    'export const resultB:number=bump(two);',
    'export const resultC:number=set(one,42);',
    'export const resultD:number=fallback(two);'
  ].join('\n'));
  function compile(path:string){
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',path,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler timed out or crashed');
    assert.notEqual(result.status,null,'compiler failed to return an exit code');
    return result;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing emitted '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {resultA,resultB,resultC,resultD} from "./entry.js";',
    'if(resultA!==24 || resultB!==25 || resultC!==42 || resultD!==25)',
    'throw Error("wrong union write runtime: "+JSON.stringify({resultA,resultB,resultC,resultD}));',
    'console.log(JSON.stringify({resultA,resultB,resultC,resultD}));'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);

  const invalid=[
    ['wrong-member','type C={tag:"a";n:number}|{tag:"b";n:number};function f(x:C):number{x.n="bad";return 0;}','TS2322'],
    ['wrong-bracket','type C={tag:"a";n:number}|{tag:"b";n:number};function f(x:C):number{x["n"]=true;return 0;}','TS2322'],
    ['readonly-arm','function f(x:{tag:"a";n:number}|{tag:"b";readonly n:number}):number{x.n=42;return 0;}','TS2540'],
    ['missing-arm','function f(x:{tag:"a";n:number}|{tag:"b";other:string}):number{x.n=42;return 0;}','TS2339'],
    ['different-types','function f(x:{tag:"a";n:number}|{tag:"b";n:string}):number{x.n=42;return 0;}','TSCS1001'],
    ['optional-arm','function f(x:{tag:"a";n?:number}|{tag:"b";n:number}):number{x.n=42;return 0;}','TSCS1001'],
    ['any-versus-number','function f(x:{tag:"a";n:any}|{tag:"b";n:number}):number{x.n="bad";return 0;}','TSCS1001'],
    ['nested-any-versus-number','function f(x:{tag:"a";n:{value:any}}|{tag:"b";n:{value:number}}):number{x.n={value:"bad"};return 0;}','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+'\n'+rejected.stderr);
    assert.match(rejected.stdout+'\n'+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,
      'invalid union property write emitted JavaScript: '+name);
  }
  console.log('Common union-property writes: four ES2015 modules executed and eight invalid programs emitted no JS.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
