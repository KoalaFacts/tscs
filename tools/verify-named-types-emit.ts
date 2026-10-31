import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const directory=mkdtempSync(join(tmpdir(),'tscs-named-types-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
function run(args:string[]) {
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{cwd:directory,
    encoding:'utf8',timeout:30000,env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler terminated by signal');
  return result;
}
try {
  writeFileSync(join(directory,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(directory,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(directory,'models.ts'),[
    'export interface Reading { value: number; label: string }',
    'export type Payload = Reading | number;',
    'export type List = (number | string)[];'
  ].join('\n'));
  writeFileSync(join(directory,'barrel.ts'),[
    'export type { Reading, Payload, List } from "./models.js";'
  ].join('\n'));
  writeFileSync(join(directory,'relay.ts'),[
    'import type { Reading } from "./barrel.js";',
    'export type { Reading as PublicReading };'
  ].join('\n'));
  writeFileSync(join(directory,'logic.ts'),[
    'import type { PublicReading as Reading } from "./relay.js";',
    'import type { Payload } from "./barrel.js";',
    'export function twice(reading: Reading): Reading { return {value: reading.value * 2, label: reading.label}; }',
    'export function pass(value: Payload): Payload { return value; }'
  ].join('\n'));
  writeFileSync(join(directory,'entry.ts'),[
    'import type { PublicReading as Reading } from "./relay.js";',
    'import type { Payload, List } from "./barrel.js";',
    'import { twice, pass } from "./logic.js";',
    'const seed: Reading = { value: 21, label: "seed" };',
    'let mixed: Payload = seed;',
    'mixed = 3;',
    'const values: List = [21, "seed"];',
    'export const result: number = twice(seed).value;',
    'export const label: string = twice(seed).label;',
    'export const count: number = values.length;'
  ].join('\n'));
  const built=run(['--emitJs','--noLib','--target','es2015','--module','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['models','barrel','relay','logic','entry'])assert.ok(existsSync(join(directory,name+'.js')),name+' not emitted');
  for(const name of ['models','barrel','relay','logic','entry']) {
    const output=readFileSync(join(directory,name+'.js'),'utf8');
    assert.doesNotMatch(output,/\b(import type|export type|interface Reading|type Payload|type List)\b/);
  }
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,count} from "./entry.js"; if(result!==42 || label!=="seed" || count!==2) throw Error("named types runtime mismatch"); console.log(JSON.stringify({result,label,count}));'],
    {cwd:directory,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  assert.deepEqual(JSON.parse(runtime.stdout),{result:42,label:'seed',count:2});
  writeFileSync(join(directory,'invalid.ts'),[
    'import type { Reading } from "./models.js";',
    'const data: Reading = { value: "bad", label: "no" };'
  ].join('\n'));
  const invalid=run(['--emitJs','--noLib','--target','es2015','--module','es2015','invalid.ts','minimal.d.ts']);
  assert.equal(invalid.status,2,invalid.stdout+invalid.stderr);
  assert.match(invalid.stdout+invalid.stderr,/TS2322/);
  assert.equal(existsSync(join(directory,'invalid.js')),false);
  console.log('Named types and type-only reexports: actual five-module JS result=42, label=seed, count=2; typed negative blocks output.');
} finally {rmSync(directory,{recursive:true,force:true});}
