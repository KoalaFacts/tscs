import {runTscs} from './tscs-process.ts';
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
  writeFileSync(join(folder,'model.ts'),'export type Input = number | string;\nexport type Handler = (n: number) => number;\nexport type Maybe = Handler | undefined | null;\nexport type ArrayOrText = number[] | string;\nexport type ReadonlyArrayOrText = readonly number[] | string;\nexport type NullableArrayOrText = number[] | null | string;\n');
  writeFileSync(join(folder,'compute.ts'),[
    'import type { Input, Maybe, ArrayOrText, ReadonlyArrayOrText, NullableArrayOrText } from "./model.js";',
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
    '}',
    'export function appendWhenArray(value: ArrayOrText): number {',
    '  if (typeof value === "object") { value.push(30); return value.length; }',
    '  return 0;',
    '}',
    'export function textWhenPrimitive(value: ArrayOrText): string {',
    '  if (typeof value !== "object") { return value; }',
    '  return "array";',
    '}',
    'export function tupleFirst(value: [number,string] | string): number {',
    '  if (typeof value === "object") { return value[0]; }',
    '  return 0;',
    '}',
    'export function readonlySize(value: ReadonlyArrayOrText): number {',
    '  if (typeof value === "object") { return value.length; }',
    '  return 0;',
    '}',
    'export function nullableSize(value: NullableArrayOrText): number {',
    '  if (typeof value === "object") { if (value !== null) { return value.length; } }',
    '  return 0;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { twice as calculate, describe, reset, invoke, missing, nullable, appendWhenArray, textWhenPrimitive, tupleFirst, readonlySize, nullableSize } from "./compute.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { calculate, describe, reset, invoke, missing, nullable, appendWhenArray, textWhenPrimitive, tupleFirst, readonlySize, nullableSize } from "./bridge.js";',
    'export const result: number = calculate(21);',
    'export const label: string = describe("ready");',
    'export const rebound: string = reset(21);',
    'export const category: string = typeof result;',
    'const double = (n: number): number => n * 2;',
    'export const called: number = invoke(double);',
    'export const absent: number = invoke(undefined);',
    'export const nullCall: number = invoke(null);',
    'export const missingValue: number = missing(undefined);',
    'export const nullValue: number = nullable(null);',
    'const list: number[] = [21];',
    'export const arraySize: number = appendWhenArray(list);',
    'export const arrayElement: number = list[1];',
    'export const stringSize: number = appendWhenArray("hello");',
    'export const textBranch: string = textWhenPrimitive("ready");',
    'export const arrayBranch: string = textWhenPrimitive([1]);',
    'export const tupleValue: number = tupleFirst([42,"ok"]);',
    'export const readonlyValue: number = readonlySize([4,5]);',
    'export const nullableNull: number = nullableSize(null);',
    'export const nullableArray: number = nullableSize([1,2,3]);'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const environment={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compiled=runTscs(folder,['--emitJs','-p',join(folder,'project.json')]);
  assert.equal(compiled.error,undefined,String(compiled.error));
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','compute','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,rebound,category,called,absent,nullCall,missingValue,nullValue,arraySize,arrayElement,stringSize,textBranch,arrayBranch,tupleValue,readonlyValue,nullableNull,nullableArray} from "./entry.js"; if(result!==42||label!=="ready"||rebound!=="reset"||category!=="number"||called!==42||absent!==0||nullCall!==0||missingValue!==9||nullValue!==5||arraySize!==2||arrayElement!==30||stringSize!==0||textBranch!=="ready"||arrayBranch!=="array"||tupleValue!==42||readonlyValue!==2||nullableNull!==0||nullableArray!==3) throw Error("typeof narrowing runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);

  writeFileSync(join(folder,'invalid.ts'),
    'function wrong(v: number | string): number { if (typeof v !== "number") { return v; } return 0; }');
  const rejected=runTscs(folder,['--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')]);
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  const rejectedArrayPrograms = [
    ['wrong-array','function wrong(v:number[]|string):void{if(typeof v==="object"){let bad:string=v;}}','TS2322'],
    ['wrong-readonly','function wrong(v:readonly number[]|string):void{if(typeof v==="object"){v.push(21);}}','TS2339'],
    ['wrong-nullable','function wrong(v:number[]|null|string):void{if(typeof v==="object"){let bad:number[]=v;}}','TS2322']
  ] as const;
  for(const [name,source,code] of rejectedArrayPrograms) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejection=runTscs(folder,['--emitJs','--noLib','--target','es2015',join(folder,name+'.ts'),join(folder,'minimal.d.ts')]);
    assert.equal(rejection.error,undefined,String(rejection.error));
    assert.equal(rejection.status,2,rejection.stdout+rejection.stderr);
    assert.match(rejection.stdout+rejection.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid typeof guard emitted JavaScript');
  }
  console.log('typeof narrowing: four-module ES2015 runtime result=42 called=42 absent=0 nullCall=0 missingValue=9 nullValue=5; wrong-branch assignment rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
