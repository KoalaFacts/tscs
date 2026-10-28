import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Runs checked emitted ES2015, not a snapshot of an expected JS string.
// This bounded test is NOT an original upstream TS7 harness acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-loop-control-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Values = number[];');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Values } from "./model.js";',
    'export function withWhile(values: Values): number {',
    '  let i: number = 0; let total: number = 0;',
    '  while (i < values.length) {',
    '    const value: number = values[i]; i++;',
    '    if (value < 0) { continue; }',
    '    switch (value) {',
    '      case 0: { continue; }',
    '      default: { break; }',
    '    }',
    '    total = total + value;',
    '    if (total > 20) { break; }',
    '  }',
    '  return total;',
    '}',
    'export function withForOf(values: Values): number {',
    '  let total: number = 0;',
    '  for (const value of values) {',
    '    if (value < 0) { continue; }',
    '    total = total + value;',
    '    if (total > 20) { break; }',
    '  }',
    '  return total;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'entry.ts'),[
    'import { withWhile, withForOf } from "./logic.js";',
    'export const result: number = withWhile([20,-1,0,1,100]) + withForOf([20,-1,1,99]);',
    'export const alternate: number = withWhile([0,5]);'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{noLib:true,target:'es2015',module:'es2015'}
  }));
  const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function compile(args:string[]) {
    const run=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{
      cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
      env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(run.error,undefined,String(run.error));
    assert.equal(run.signal,null,'compiler timed out or crashed');
    return run;
  }
  const compiled=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(compiled.status,0,compiled.stdout+'\n'+compiled.stderr);
  for(const name of ['model','logic','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),name+' JavaScript was not emitted');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,alternate} from "./entry.js"; '+
    'if(result!==42 || alternate!==5) throw Error("wrong while/continue/break runtime: "+result+"/"+alternate);'
  ],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+'\n'+run.stderr);
  writeFileSync(join(folder,'wrong.ts'),
    'function wrong():number { let i:number=0; while(i<1){let s:string=1;i++;break;}return i; }');
  const rejected=compile(['--emitJs','--noLib','--target','es2015','wrong.ts','minimal.d.ts']);
  assert.equal(rejected.status,2,rejected.stdout+'\n'+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'wrong.js')),false);
  writeFileSync(join(folder,'bad-continue.ts'),
    'function bad(n:number):number { continue; return n; }');
  const outside=compile(['--emitJs','--noLib','--target','es2015','bad-continue.ts','minimal.d.ts']);
  assert.notEqual(outside.status,0,'continue without an enclosing loop succeeded');
  assert.match(outside.stdout+outside.stderr,/TSCS1001/);
  assert.equal(existsSync(join(folder,'bad-continue.js')),false);
  console.log('Generated three-module ES2015 executed: result=42, alternate=5; wrong assignment and out-of-loop continue blocked output.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
