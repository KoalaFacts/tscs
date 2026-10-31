import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module runtime and negative no-emit, not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-conditional-value-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Packet = { kind: "ok"; value: number } | { kind: "bad"; message: string };\n');
  writeFileSync(join(folder,'choose.ts'),[
    'import type { Packet } from "./model.js";',
    'export function choose(packet: Packet): number {',
    '  return packet.kind === "ok" ? packet.value * 2 : 0;',
    '}',
    'export function describe(packet: Packet): string {',
    '  return packet.kind === "bad" ? packet.message : "ready";',
    '}',
    'let calls: number = 0;',
    'function hit(): number { calls += 1; return 100; }',
    'export function lazy(flag: boolean): number {',
    '  const result: number = flag ? 42 : hit();',
    '  return result + calls;',
    '}',
    'export function unbraced(flag: boolean): number {',
    '  let value: number = 0;',
    '  if (flag) value = 21; else value = 0;',
    '  return value;',
    '}',
    'export function dangling(a: boolean, b: boolean): number {',
    '  if (a) if (b) return 21; else return 10;',
    '  return 0;',
    '}',
    'export function early(n: number): number {',
    '  if (n) return 42;',
    '  return 0;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { choose, describe, lazy, unbraced, dangling, early } from "./choose.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { choose, describe, lazy, unbraced, dangling, early } from "./bridge.js";',
    'export const result: number = choose({ kind: "ok", value: 21 });',
    'export const other: number = choose({ kind: "bad", message: "bad" });',
    'export const label: string = describe({ kind: "bad", message: "bad" });',
    'export const shortCircuit: number = lazy(true);',
    'export const unbracedYes: number = unbraced(true);',
    'export const unbracedNo: number = unbraced(false);',
    'export const danglingTrue: number = dangling(true, true);',
    'export const danglingFalse: number = dangling(true, false);',
    'export const danglingOuter: number = dangling(false, true);',
    'export const earlyYes: number = early(1);',
    'export const earlyNo: number = early(0);'
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
  for(const name of ['model','choose','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,other,label,shortCircuit,unbracedYes,unbracedNo,danglingTrue,danglingFalse,danglingOuter,earlyYes,earlyNo} from "./entry.js"; if(result!==42||other!==0||label!=="bad"||shortCircuit!==42||unbracedYes!==21||unbracedNo!==0||danglingTrue!==21||danglingFalse!==10||danglingOuter!==0||earlyYes!==42||earlyNo!==0) throw Error("conditional value runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  writeFileSync(join(folder,'invalid.ts'),
    'function wrong(flag: boolean): number { return flag ? "wrong" : 0; }');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  writeFileSync(join(folder,'invalid-unbraced.ts'),
    'function wrong(flag: boolean): number { if (flag) const value = 1; return 0; }');
  const invalidUnbraced=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid-unbraced.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(invalidUnbraced.error,undefined,String(invalidUnbraced.error));
  assert.equal(invalidUnbraced.status,1,invalidUnbraced.stdout+invalidUnbraced.stderr);
  assert.match(invalidUnbraced.stdout+invalidUnbraced.stderr,/TSCS1001/);
  assert.equal(existsSync(join(folder,'invalid-unbraced.js')),false);
  console.log('conditional values: four-module ES2015 runtime result=42 other=0 label=bad shortCircuit=42; invalid branch rejected without emit.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
