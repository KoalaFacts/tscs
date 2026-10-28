import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Real four-module JS execution of compound narrowing and short-circuiting.
// Bounded implementation proof; not original TS7 harness acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-compound-guards-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Packet = { kind: "ok"; value: number } | { kind: "bad"; message: string };\n');
  writeFileSync(join(folder,'rules.ts'),[
    'import type { Packet } from "./model.js";',
    'export function calculate(packet: Packet): number {',
    '  if (packet.kind === "ok" && packet.value > 0) { return packet.value * 2; }',
    '  return 0;',
    '}',
    'export function fallback(packet: Packet): string {',
    '  if (packet.kind === "ok" || packet.message === "bad") { return "yes"; }',
    '  return "no";',
    '}',
    'export function negated(packet: Packet): number {',
    '  if (!(packet.kind !== "ok")) { return packet.value; }',
    '  return 0;',
    '}',
    'let visits: number = 0;',
    'function hit(): boolean { visits += 1; return true; }',
    'export function lazy(packet: Packet): number {',
    '  let accepted: boolean = packet.kind === "ok" && hit();',
    '  return accepted ? visits + 41 : visits;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { calculate, fallback, negated, lazy } from "./rules.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { calculate, fallback, negated, lazy } from "./bridge.js";',
    'export const result: number = calculate({ kind: "ok", value: 21 });',
    'export const other: number = calculate({ kind: "bad", message: "bad" });',
    'export const label: string = fallback({ kind: "bad", message: "bad" });',
    'export const selected: number = negated({ kind: "ok", value: 21 });',
    'export const skipped: number = lazy({ kind: "bad", message: "bad" });',
    'export const executed: number = lazy({ kind: "ok", value: 2 });'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compile=spawnSync(process.env.DOTNET??'dotnet',[compiler,'--emitJs','-p',join(folder,'project.json')],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(compile.error,undefined,String(compile.error));
  assert.equal(compile.status,0,compile.stdout+compile.stderr);
  for(const name of ['model','rules','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,other,label,selected,skipped,executed} from "./entry.js"; if(result!==42||other!==0||label!=="yes"||selected!==21||skipped!==0||executed!==42) throw Error("compound runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  writeFileSync(join(folder,'wrong.ts'),
    'type P = { kind: "ok"; value: number } | { kind: "bad"; message: string }; function wrong(p: P): number { if (p.kind === "ok" && p.message === "bad") { return 1; } return 0; }');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'wrong.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2339/);
  assert.equal(existsSync(join(folder,'wrong.js')),false);
  console.log('compound guards: four-module runtime result=42 other=0 selected=21 label=yes, short-circuit skipped=0 executed=42; wrong-member no-emit.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
