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
    'export function unbraced(value: number): number {',
    '  let total: number = 0;',
    '  switch (value) { case 1: total = 20; case 2: total = total + 1; break; default: total = 0; }',
    '  return total;',
    '}',
    'export function grouped(value: number): number {',
    '  switch (value) { case 1: case 2: return 21; default: return 0; }',
    '}',
    'export function typed(e: Event): number {',
    '  switch(e.kind) { case "ok": return e.value; default: return 0; }',
    '}',
    'export function first(values: number[]): number {',
    '  let result: number = 0;',
    '  for (const value of values) { result = result + value; break; }',
    '  return result;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'entry.ts'),[
    'import { dispatch, fall, first, unbraced, grouped, typed } from "./logic.js";',
    'export const result: number = dispatch({kind:"ok",value:21}) + fall(1);',
    'export const alternate: number = fall(2);',
    'export const firstValue: number = first([21,1]);',
    'export const bareFall: number = unbraced(1);',
    'export const bareDirect: number = unbraced(2);',
    'export const emptyGroup: number = grouped(2);',
    'export const typedArm: number = typed({kind:"ok",value:42});'
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
    'import {result,alternate,firstValue,bareFall,bareDirect,emptyGroup,typedArm} from "./entry.js"; '+
    'if(result!==42 || alternate!==1 || firstValue!==21 || bareFall!==21 || bareDirect!==1 || emptyGroup!==21 || typedArm!==42) throw Error("switch/loop control-flow runtime mismatch");'
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
  for(const [filename,source,expectedCode] of [
    ['bad-unbraced.ts',
      'type E={kind:"ok";value:number}|{kind:"error";message:string}; '+
      'function bad(e:E):number {switch(e.kind){case "error": return e.value; default: return 0;}}',
      'TS2339'],
    ['bad-lexical.ts',
      'function bad(n:number):number {switch(n){case 1: let x:number=1; return x; default: return 0;}}',
      'TSCS1001']
  ] as const) {
    writeFileSync(join(folder,filename),source);
    const invalid=compile(['--emitJs','--noLib','--target','es2015',filename,'minimal.d.ts']);
    assert.equal(invalid.status,2,invalid.stdout+'\n'+invalid.stderr);
    assert.match(invalid.stdout+invalid.stderr,new RegExp(expectedCode));
    assert.equal(existsSync(join(folder,filename.replace('.ts','.js'))),false);
  }
  console.log('Checked braced/unbraced switch break, fallthrough, grouped labels, typed branches and for..of break actually executed; invalid branches and unsupported lexical case rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
