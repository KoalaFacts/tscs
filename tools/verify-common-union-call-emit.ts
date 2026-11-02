import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Product compiler -> four ES2015 modules -> real Node execution.
const folder=mkdtempSync(join(tmpdir(),'tscs-union-call-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    "export type Choice={tag:'number';run(n:number):number}|{tag:'string';run(n:number):string};",
    "export const numeric:Choice={tag:'number',run:(n:number):number=>n+1};",
    "export const textual:Choice={tag:'string',run:(n:number):string=>'value'+n};"
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    "import type {Choice} from './model.js';",
    "export function apply(x:Choice):number|string {return x.run(41);}",
    "export function applyBracket(x:Choice):number|string {return x['run'](2);}"
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),"export {apply,applyBracket} from './logic.js';\n");
  writeFileSync(join(folder,'entry.ts'),[
    "import {numeric,textual} from './model.js';",
    "import {apply,applyBracket} from './bridge.js';",
    "export const a:number|string=apply(numeric);",
    "export const b:number|string=apply(textual);",
    "export const c:number|string=applyBracket(numeric);",
    "export const d:number|string=applyBracket(textual);"
  ].join('\n'));
  function compile(name:string){
    const run=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',name,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(run.error,undefined,String(run.error));
    assert.equal(run.signal,null,'compiler crashed or timed out');
    assert.notEqual(run.status,null);
    return run;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const file of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,file+'.js')),'missing emitted '+file+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',[
    "import {a,b,c,d} from './entry.js';",
    "assert(a===42 && b==='value41' && c===3 && d==='value2');",
    "function assert(ok){if(!ok)throw Error(JSON.stringify({a,b,c,d}));}"
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+'\n'+run.stderr);
  const invalid=[
    ['wrong-arg',"type C={run(n:number):number}|{run(n:number):string};function f(x:C):number|string{return x.run('bad');}",'TS2345'],
    ['missing-arg',"type C={run(n:number):number}|{run(n:number):string};function f(x:C):number|string{return x.run();}",'TS2554'],
    ['extra-arg',"type C={run(n:number):number}|{run(n:number):string};function f(x:C):number|string{return x.run(1,2);}",'TS2554'],
    ['bad-result',"type C={run(n:number):number}|{run(n:number):string};function f(x:C):number{return x.run(1);}",'TS2322'],
    ['missing-member',"type C={run(n:number):number}|{other(n:number):string};function f(x:C):number{return x.run(1);}",'TS2339'],
    ['different-parameters',"type C={run(n:number):number}|{run(n:string):string};function f(x:C):number|string{return x.run(1);}",'TSCS1001'],
    ['any-versus-number',"type C={run(n:any):string}|{run(n:number):number};function f(x:C):number|string{return x.run('bad');}",'TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+'\n'+rejected.stderr);
    assert.match(rejected.stdout+'\n'+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid source emitted '+name+'.js');
  }
  console.log('Common union calls: four linked ES2015 modules executed; seven invalid programs rejected without JS.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
