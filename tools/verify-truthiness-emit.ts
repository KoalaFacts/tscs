import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Checked four-module ES2015 execution, not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-truthy-guard-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export interface Box { value: number }',
    'export type Maybe = Box | null | undefined;',
    'export type Handler = (n:number)=>number;',
    'export type MaybeHandler = Handler | undefined;'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Maybe,MaybeHandler} from "./model.js";',
    'export function read(value:Maybe):number{if(value){return value.value;}return 0;}',
    'export function inverse(value:Maybe):number{if(!value){return 0;}return value.value;}',
    'export function invoke(fn:MaybeHandler):number{if(!fn){return 0;}return fn(21);}',
    'export function choose(value:Maybe):number{return value?value.value:0;}',
    'export function number(value:number|null):number{if(value){return value*2;}return 0;}',
    'export function label(value:string|null):string{return value?value:"missing";}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),
    'export {read,inverse,invoke,choose,number,label} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {read,inverse,invoke,choose,number,label} from "./bridge.js";',
    'const twice=(n:number):number=>n*2;',
    'export const hit:number=read({value:42});',
    'export const nullMiss:number=read(null);',
    'export const undefinedMiss:number=read(undefined);',
    'export const inverseHit:number=inverse({value:21});',
    'export const inverseMiss:number=inverse(null);',
    'export const called:number=invoke(twice);',
    'export const notCalled:number=invoke(undefined);',
    'export const fromTernary:number=choose({value:32});',
    'export const ternaryAbsent:number=choose(undefined);',
    'export const zero:number=number(0);',
    'export const doubled:number=number(21);',
    'export const empty:string=label("");',
    'export const nonempty:string=label("ready");'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compile=(args:string[])=>{
    const result=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{
      cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env
    });
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler failed or timed out');
    return result;
  };
  const built=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const actual=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {hit,nullMiss,undefinedMiss,inverseHit,inverseMiss,called,notCalled,fromTernary,ternaryAbsent,zero,doubled,empty,nonempty} from "./entry.js";',
    'if(hit!==42||nullMiss!==0||undefinedMiss!==0||inverseHit!==21||inverseMiss!==0||called!==42||notCalled!==0||fromTernary!==32||ternaryAbsent!==0||zero!==0||doubled!==42||empty!=="missing"||nonempty!=="ready")',
    ' throw Error("truthiness guard runtime mismatch");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(actual.error,undefined,String(actual.error));
  assert.equal(actual.status,0,actual.stdout+actual.stderr);
  const invalid=[
    ['wrong-true','function bad(v:number|string):number{if(v){return v;}return 0;}','TS2322'],
    ['wrong-false','function bad(v:number|string):number{if(!v){return v;}return 0;}','TS2322'],
    ['wrong-ternary','function bad(v:number|string):number{return v?v:0;}','TS2322'],
    ['write-invalidate','function bad(v:{value:number}|undefined):number{if(v){v=undefined;return v.value;}return 0;}','TS18048']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted JS');
  }
  console.log('Finite truthiness guards: four-module ES2015 runtime executed; four invalid programs blocked.');
} finally {rmSync(folder,{recursive:true,force:true});}
