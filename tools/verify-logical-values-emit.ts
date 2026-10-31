import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Execute four real linked ES2015 modules; no original TS7 acceptance credit.
const folder=mkdtempSync(join(tmpdir(),'tscs-logical-values-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
const compile=(args:string[])=>{
  const r=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.signal,null,'compiler signal/timeout');
  return r;
};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export interface Box {value:number}\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Box} from "./model.js";',
    'export function andValue(n:number|null):number|null{return n && n+1;}',
    'export function orValue(n:number|null):number{return n || 42;}',
    'export function orString(s:string|null):string{return s || "fallback";}',
    'export function andFlag(flag:boolean,n:number):number|false{return flag && n;}',
    'export function orFlag(flag:boolean,n:number):number|true{return flag || n;}',
    'export function fromBox(box:Box|null):number|null{return box && box.value;}',
    'export function fallback(box:Box|null):Box{return box || {value:21};}',
    'export function leftOnce(next:()=>number):number{return next() || 21;}',
    'export function lazy(flag:boolean,next:()=>number):number|false{return flag && next();}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {andValue,orValue,orString,andFlag,orFlag,fromBox,fallback,leftOnce,lazy} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {andValue,orValue,orString,andFlag,orFlag,fromBox,fallback,leftOnce,lazy} from "./bridge.js";',
    'let leftCalls:number=0;let rightCalls:number=0;',
    'function left():number{leftCalls=leftCalls+1;return 0;}',
    'function right():number{rightCalls=rightCalls+1;return 42;}',
    'export const result=[andValue(null),andValue(3),orValue(null),orValue(0),orValue(3),orString(null),orString(""),orString("ok"),andFlag(false,42),andFlag(true,42),orFlag(true,42),orFlag(false,42),fromBox(null),fromBox({value:9}),fallback(null).value,fallback({value:5}).value,leftOnce(left),lazy(false,right),lazy(true,right),leftCalls,rightCalls];'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const built=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])assert.ok(existsSync(join(folder,name+'.js')),'missing '+name);
  const run=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result} from "./entry.js";',
    'const expected=[null,4,42,42,3,"fallback","fallback","ok",false,42,true,42,null,9,21,5,21,false,42,1,1];',
    'if(JSON.stringify(result)!==JSON.stringify(expected))throw Error(JSON.stringify({result,expected}));'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const invalid=[
    ['null-and','function wrong(n:number|null):number{return n && n+1;}','TS2322'],
    ['bad-result','function wrong(n:number|null):string{return n || 21;}','TS2322'],
    ['bad-member','function wrong(box:{value:number}|null):number{return box && box.missing;}','TS2339'],
    ['nonboolean','function wrong(v:number|string):boolean{return typeof v === "number" && v;}','TS2322'],
    ['void-logical','function noop():void{return;}function wrong():void{return noop() && noop();}','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const r=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(r.status,2,r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid JS output: '+name);
  }
  console.log('Logical values: four ES2015 modules executed; once-only/lazy evaluation; five invalid cases blocked.');
} finally {rmSync(folder,{recursive:true,force:true});}
