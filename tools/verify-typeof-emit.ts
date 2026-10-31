import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Checked cross-module runtime, not original TS7 configuration acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-typeof-primitive-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Input = number | string;\nexport type Handler = (n: number) => number;\nexport type Maybe = Handler | undefined | null;\n');
  writeFileSync(join(folder,'compute.ts'),[
    'import type { Input, Maybe } from "./model.js";',
    'export function twice(value: Input): number {',
    '  if (typeof value !== "number") { return 0; }',
    '  return value * 2;',
    '}',
    'export function describe(value: Input): string {',
    '  if (typeof value === "string") { return value; }',
    '  return "number";',
    '}',
    'export function reset(value: Input): string {',
    '  if (typeof value !== "number") { return value; }',
    '  value = "reset";',
    '  return value;',
    '}',
    'export function invoke(value: Maybe): number {',
    '  if (typeof value !== "function") { return 0; }',
    '  return value(21);',
    '}',
    'export function missing(value: number | undefined): number {',
    '  if (typeof value === "undefined") { return 9; }',
    '  return value;',
    '}',
    'export function nullable(value: number | null): number {',
    '  if (typeof value === "object") { return 5; }',
    '  return value;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { twice as calculate, describe, reset, invoke, missing, nullable } from "./compute.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { calculate, describe, reset, invoke, missing, nullable } from "./bridge.js";',
    'export const result: number = calculate(21);',
    'export const label: string = describe("ready");',
    'export const rebound: string = reset(21);',
    'export const category: string = typeof result;',
    'const double = (n: number): number => n * 2;',
    'export const called: number = invoke(double);',
    'export const absent: number = invoke(undefined);',
    'export const nullCall: number = invoke(null);',
    'export const missingValue: number = missing(undefined);',
    'export const nullValue: number = nullable(null);'
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
  for(const name of ['model','compute','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,rebound,category,called,absent,nullCall,missingValue,nullValue} from "./entry.js"; if(result!==42||label!=="ready"||rebound!=="reset"||category!=="number"||called!==42||absent!==0||nullCall!==0||missingValue!==9||nullValue!==5) throw Error("typeof narrowing runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);

  writeFileSync(join(folder,'invalid.ts'),
    'function wrong(v: number | string): number { if (typeof v !== "number") { return v; } return 0; }');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  console.log('typeof narrowing: four-module ES2015 runtime result=42 called=42 absent=0 nullCall=0 missingValue=9 nullValue=5; wrong-branch assignment rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
