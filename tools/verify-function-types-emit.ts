import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const dir=mkdtempSync(join(tmpdir(),'tscs-function-types-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(dir,'model.ts'),[
    'export type Callback=(value:number)=>number;',
    'export type Invoker=(cb:Callback,value:number)=>number;',
    'export interface Box { handler:Callback; }',
    'export interface MethodBox { handle(n:number):number; }'
  ].join('\n'));
  writeFileSync(join(dir,'logic.ts'),[
    'import type {Callback,Invoker,MethodBox} from "./model.js";',
    'export function double(value:number):number{return value+value;}',
    'export function apply(cb:Callback,value:number):number{return cb(value);}',
    'export const executor:Invoker=apply;',
    'export const methodBox:MethodBox={handle:double};'
  ].join('\n'));
  writeFileSync(join(dir,'bridge.ts'),'export {double as operation,executor,methodBox} from "./logic.js";\n');
  writeFileSync(join(dir,'entry.ts'),[
    'import type {Callback,Box} from "./model.js";',
    'import {operation,executor,methodBox} from "./bridge.js";',
    'const selected:Callback=operation;',
    'const box:Box={handler:selected};',
    'const extracted:Callback=box.handler;',
    'export const result:number=executor(extracted,21);',
    'const selectedMethod:(n:number)=>number=methodBox.handle;',
    'export const methodResult:number=executor(selectedMethod,21);'
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
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing '+name+' JavaScript');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,methodResult} from "./entry.js"; if(result!==42||methodResult!==42)throw Error("typed callback or method runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['nonfunction','type Fn=(n:number)=>number;let cb:Fn=42;','TS2322',2],
    ['bad-parameter','type Fn=(n:number)=>number;function bad(n:string):number{return 42;}let cb:Fn=bad;','TS2322',2],
    ['bad-return','type Fn=(n:number)=>number;function bad(n:number):string{return "bad";}let cb:Fn=bad;','TS2322',2],
    ['wrong-call','type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let x:number=cb("bad");','TS2345',2],
    ['bad-arity','type Fn=(n:number)=>number;function twice(n:number):number{return n+n;}let cb:Fn=twice;let x:number=cb();','TS2554',2],
    ['required-after-optional','type Bad=(n?:number,other:number)=>number;','TSCS1001',1],
    ['bad-rest','type Bad=(...values:number)=>number;','TSCS1001',1],
    ['untyped-arrow-unsupported','const lambda=(n)=>n;','TSCS1001',1],
    ['method-return','interface Op{double(n:number):number}const op:Op={double:(n:number):string=>"bad"};','TS2322',2],
    ['method-argument','interface Op{double(n:number):number}const op:Op={double:(n:number)=>n+n};const cb:(n:number)=>number=op.double;cb("bad");','TS2345',2],
    ['method-arity','interface Op{double(n:number):number}const op:Op={double:(n:number)=>n+n};const cb:(n:number)=>number=op.double;cb();','TS2554',2]
  ];
  for(const [name,code,expected,exit] of invalid) {
    const file=join(dir,'bad-'+name+'.ts');
    writeFileSync(file,code);
    const checked=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(checked.status,exit,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(expected));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'invalid program emitted JS');
  }
  console.log('typed function contracts: four-module ES2015 JavaScript result=42; eleven negative no-emit programs rejected.');
} finally { rmSync(dir,{recursive:true,force:true}); }
