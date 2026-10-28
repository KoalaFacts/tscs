import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module checked ES2015 execution and negative no-emit; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-bitwise-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Packet = { left: number; right: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Packet } from "./model.js";',
    'export function combine(data: Packet): number {',
    '  return ((data.left & 15) << 2) | (data.right ^ 7);',
    '}',
    'export function mask(n: number): number { return ~n & 255; }',
    'export function shift(n: number): number { return n >>> 1; }',
    'export function mutate(data: Packet): number {',
    '  data.left &= 15;',
    '  data.left <<= 2;',
    '  data.right ^= 7;',
    '  data.left |= data.right;',
    '  data.left >>= 1;',
    '  data.left <<= 1;',
    '  data.right >>>= 1;',
    '  return data.left + data.right;',
    '}',
    'export function indexed(): number {',
    '  let values: number[] = [7, 3];',
    '  values[0] &= 6;',
    '  values[0] |= 1;',
    '  values[0] ^= 2;',
    '  values[0] <<= 2;',
    '  values[0] >>= 1;',
    '  values[0] >>>= 1;',
    '  return values[0];',
    '}',
    'export function iterations(): number {',
    '  let count: number = 0;',
    '  for (let i: number = 16; i > 0; i >>= 1) { count += 1; }',
    '  return count;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { combine, mask, shift, mutate, indexed, iterations } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { combine, mask, shift, mutate, indexed, iterations } from "./bridge.js";',
    'export const result: number = combine({ left: 10, right: 5 });',
    'export const masked: number = mask(0);',
    'export const shifted: number = shift(-2);',
    'export const priority: number = 1 | 2 & 4;',
    'export const addFirst: number = 1 << 3 + 2;',
    'export const mutated: number = mutate({ left: 10, right: 5 });',
    'export const indexedResult: number = indexed();',
    'export const iterationCount: number = iterations();'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const environment={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compiled=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','-p',join(folder,'project.json')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(compiled.error,undefined,String(compiled.error));
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,masked,shifted,priority,addFirst,mutated,indexedResult,iterationCount} from "./entry.js"; if(result!==42||masked!==255||shifted!==2147483647||priority!==1||addFirst!==32||mutated!==43||indexedResult!==5||iterationCount!==5) throw Error("numeric bitwise runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  writeFileSync(join(folder,'invalid.ts'),'let invalid: string = 3 & 2;');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  // A rejected compound mutation must not leave partial JavaScript behind.
  writeFileSync(join(folder,'invalid-mutation.ts'),'const value: number = 7; value &= 3;');
  const rejectedMutation=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid-mutation.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejectedMutation.error,undefined,String(rejectedMutation.error));
  assert.equal(rejectedMutation.status,2,rejectedMutation.stdout+rejectedMutation.stderr);
  assert.match(rejectedMutation.stdout+rejectedMutation.stderr,/TS2588/);
  assert.equal(existsSync(join(folder,'invalid-mutation.js')),false);
  console.log('numeric bitwise: four-module ES2015 runtime result=42 mutated=43 indexed=5 iterations=5; invalid assignment refused without output.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
