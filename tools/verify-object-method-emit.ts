import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Bounded real ES2015 output execution, not original upstream TS7 acceptance.
const dir=mkdtempSync(join(tmpdir(),'tscs-object-method-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  const sources={
    'model.ts':'export interface Op{twice(n:number):number}',
    'logic.ts':[
      'import type {Op} from "./model.js";',
      'export const op:Op={twice(n:number):number{return n+n;}};'
    ].join('\n'),
    'bridge.ts':'export {op} from "./logic.js";',
    'entry.ts':[
      'import {op} from "./bridge.js";',
      'export const result:number=op.twice(21);',
      'const nested={inner:{twice(n:number){return n+n;}}};',
      'export const nestedResult:number=nested.inner.twice(21);',
      'const more={add(base:number,...items:number[]):number{let total:number=base;for(const item of items){total+=item;}return total;}};',
      'export const restResult:number=more.add(20,21,1);',
      'const defaults={twice(n:number=21):number{return n+n;}};',
      'export const defaultResult:number=defaults.twice();',
      'const named={"twice"(n:number){return n+n;}};',
      'export const namedResult:number=named.twice(21);',
      'const callback:(n:number)=>number=op.twice;',
      'export const callbackResult:number=callback(21);'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(sources))
    writeFileSync(join(dir,name),source+'\n');
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]){
    const r=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing output '+name);
  const logic=readFileSync(join(dir,'logic.js'),'utf8');
  assert.match(logic,/twice\(n\)\s*\{/,'must emit native ES2015 method shorthand');
  assert.doesNotMatch(logic,/twice:\s*function/,'must not replace a method with a function property');
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,nestedResult,restResult,defaultResult,namedResult,callbackResult} from "./entry.js"; import {op} from "./logic.js"; if([result,nestedResult,restResult,defaultResult,namedResult,callbackResult].some(x=>x!==42))throw Error("object method runtime mismatch"); if(Object.hasOwn(op.twice,"prototype"))throw Error("method incorrectly became a constructible function");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid:[string,string,string,number][]=[
    ['return','const op={run(n:number):number{return "bad";}};','TS2322',2],
    ['argument','const op={run(n:number):number{return n;}};op.run("bad");','TS2345',2],
    ['arity','const op={run(n:number):number{return n;}};op.run();','TS2554',2],
    ['name','const op={run(n:number):number{return missingName;}};','TS2304',2],
    ['flow','const op={run(n:number):number{if(n>0){return n;}}};','TSCS1001',1],
    ['untyped','const op={run(n){return n;}};','TSCS1001',1]
  ];
  for(const [id,source,code,exit] of invalid){
    const path=join(dir,'invalid-'+id+'.ts');
    writeFileSync(path,source);
    const checked=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(checked.status,exit,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid emitted JavaScript: '+id);
  }
  console.log('object methods: 4-module ES2015 runtime, method identity, and 6 fail-closed no-emit programs checked');
} finally {
  rmSync(dir,{recursive:true,force:true});
}
