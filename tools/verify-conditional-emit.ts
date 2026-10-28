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
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { choose, describe, lazy } from "./choose.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { choose, describe, lazy } from "./bridge.js";',
    'export const result: number = choose({ kind: "ok", value: 21 });',
    'export const other: number = choose({ kind: "bad", message: "bad" });',
    'export const label: string = describe({ kind: "bad", message: "bad" });',
    'export const shortCircuit: number = lazy(true);'
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
    'import {result,other,label,shortCircuit} from "./entry.js"; if(result!==42||other!==0||label!=="bad"||shortCircuit!==42) throw Error("conditional value runtime mismatch");'],
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
  console.log('conditional values: four-module ES2015 runtime result=42 other=0 label=bad shortCircuit=42; invalid branch rejected without emit.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
