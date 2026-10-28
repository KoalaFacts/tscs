import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// End-to-end C# compilation and execution; original TS7 acceptance stays separate.
const folder=mkdtempSync(join(tmpdir(),'tscs-switch-completion-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),
    'export type Event = { kind: "ok"; value: number } | { kind: "error"; message: string };');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Event } from "./model.js";',
    'export function dispatch(e: Event): number {',
    '  let amount: number = 0;',
    '  switch (e.kind) {',
    '    case "ok": { amount = e.value; break; }',
    '    case "error": { amount = 0; break; }',
    '    default: { break; }',
    '  }',
    '  return amount;',
    '}',
    'export function fall(value: number): number {',
    '  let total: number = 0;',
    '  switch(value) {',
    '    case 1: { total = 20; }',
    '    case 2: { total = total + 1; break; }',
    '    default: { break; }',
    '  }',
    '  return total;',
    '}',
    'export function first(values: number[]): number {',
    '  let result: number = 0;',
    '  for (const value of values) { result = result + value; break; }',
    '  return result;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'entry.ts'),[
    'import { dispatch, fall, first } from "./logic.js";',
    'export const result: number = dispatch({kind:"ok",value:21}) + fall(1);',
    'export const alternate: number = fall(2);',
    'export const firstValue: number = first([21,1]);'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{noLib:true,target:'es2015',module:'es2015'}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function compile(args:string[]) {
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{
      cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
      env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null);
    return result;
  }
  const compiled=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(compiled.status,0,compiled.stdout+'\n'+compiled.stderr);
  for(const name of ['model','logic','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),name+' was not emitted');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,alternate,firstValue} from "./entry.js"; '+
    'if(result!==42 || alternate!==1 || firstValue!==21) throw Error("switch/loop control-flow runtime mismatch");'
  ],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);
  writeFileSync(join(folder,'bad.ts'),
    'type E={kind:"ok";value:number}|{kind:"error";message:string}; '+
    'function bad(e:E):number { switch(e.kind){ case "ok": {} '+
    'case "error": {return e.value;} default: {return 0;} } }');
  const rejected=compile(['--emitJs','--noLib','--target','es2015','bad.ts','minimal.d.ts']);
  assert.equal(rejected.status,2,rejected.stdout+'\n'+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2339/);
  assert.equal(existsSync(join(folder,'bad.js')),false);
  console.log('Checked switch break/fallthrough and for..of break actually executed: result=42, alternate=1, firstValue=21; invalid fallthrough member rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
