import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module ES2015 execution; assertions/constraints erase only after checking.
const folder=mkdtempSync(join(tmpdir(),'tscs-type-operators-'));
const dotnet=process.env.DOTNET??'dotnet';
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function run(args:string[]) {
  const result=spawnSync(dotnet,[compiler,...args],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item = { value: number; label: string }; export type Variant = { kind: "a"; value: number } | { kind: "b"; label: string }; export type Pair = [first: number, second: string]; export type NestedPair = [inner: readonly [count: number, string], { valid: boolean }];\n');
  writeFileSync(join(folder,'factory.ts'),[
    'import type { Item, Variant, Pair, NestedPair } from "./model.js";',
    'export function fromAny(value: any): Item {',
    '  return (value as Item) satisfies Item;',
    '}',
    'export function make(value: number): Item {',
    '  const item = { value: value, label: "ok" } satisfies Item;',
    '  return item;',
    '}',
    'export function makeA(value: number): { kind: "a"; value: number } {',
    '  return { kind: "a", value: value } satisfies Variant;',
    '}',
    'export function makeB(label: string): { kind: "b"; label: string } {',
    '  return { kind: "b", label: label } satisfies Variant;',
    '}',
    'export const immutable = { kind: "ready", nested: { value: 21, valid: true } } as const;',
    'export const immutableTuple = [21, "go"] as const;',
    'export const nestedTuple = { items: [true, [42]] } as const;',
    'export function makePair(n: number, s: string): Pair { const p: Pair = [n, s]; p[0]++; return p; }',
    'export function makeNestedPair(n: number): NestedPair { return [[n, "go"], { valid: true }]; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { fromAny, make, makeA, makeB, immutable, immutableTuple, nestedTuple, makePair, makeNestedPair } from "./factory.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { fromAny, make, makeA, makeB, immutable, immutableTuple, nestedTuple, makePair, makeNestedPair } from "./bridge.js";',
    'import type { Pair, NestedPair } from "./model.js";',
    'const raw: any = { value: 21, label: "ready" };',
    'const item = fromAny(raw);',
    'export const result: number = make(item.value).value * 2;',
    'export const label: string = item.label;',
    'export const extra: number = ({ value: 42, extra: 3 } as { value: number }).value;',
    'export const exact: 21 = (21 as 21);',
    'export const checked: string = "ready" satisfies string;',
    'export const advanced: number = ((21 as number) satisfies number) + 21;',
    'export const selected: number = makeA(23).value;',
    'export const selectedTag: "a" = makeA(23).kind;',
    'export const otherLabel: string = makeB("tagged").label;',
    'export const readonlyTag: "ready" = immutable.kind;',
    'export const readonlyValue: 21 = immutable.nested.value;',
    'export const readonlyValid: true = immutable.nested.valid;',
    'export const tupleFirst: 21 = immutableTuple[0];',
    'export const tupleSecond: "go" = immutableTuple[1];',
    'export const tupleLength: 2 = immutableTuple.length;',
    'export const nestedTupleValue: 42 = nestedTuple.items[1][0];',
    'const namedPair: Pair = makePair(20, "ready");',
    'const namedNested: NestedPair = makeNestedPair(21);',
    'export const namedTupleNumber: number = namedPair[0];',
    'export const namedTupleText: string = namedPair[1];',
    'export const namedNestedNumber: number = namedNested[0][0];',
    'export const namedNestedValid: boolean = namedNested[1].valid;'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiled=run(['--emitJs','-p',join(folder,'project.json')]);
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','factory','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,extra,exact,checked,advanced,selected,selectedTag,otherLabel,readonlyTag,readonlyValue,readonlyValid,tupleFirst,tupleSecond,tupleLength,nestedTupleValue,namedTupleNumber,namedTupleText,namedNestedNumber,namedNestedValid} from "./entry.js"; if(result!==42||label!=="ready"||extra!==42||exact!==21||checked!=="ready"||advanced!==42||selected!==23||selectedTag!=="a"||otherLabel!=="tagged"||readonlyTag!=="ready"||readonlyValue!==21||readonlyValid!==true||tupleFirst!==21||tupleSecond!=="go"||tupleLength!==2||nestedTupleValue!==42||namedTupleNumber!==21||namedTupleText!=="ready"||namedNestedNumber!==21||namedNestedValid!==true) throw Error("type-operator JS runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  for(const [name,source,code] of [
    ['invalid-assert','let value: number = 21; let s = value as string;','TS2352'],
    ['invalid-satisfies','const value = "wrong" satisfies number;','TS1360'],
    ['invalid-fresh','const bad = {value: 21, excess: 2} satisfies {value: number};','TS2353'],
    ['invalid-tag-write',"type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: 21 } satisfies V; p.kind = \"b\";",'TS2322'],
    ['invalid-tag-branch',"type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: \"bad\" } satisfies V;",'TS2322'],
    ['invalid-extra-branch',"type V = { kind: \"a\"; value: number } | { kind: \"b\"; label: string }; let p = { kind: \"a\", value: 21, label: \"wrong\" } satisfies V;",'TS2353'],
    ['readonly-write','const p={kind:"ready"} as const;p.kind="later";','TS2540'],
    ['nested-readonly-write','const p={nested:{value:21}} as const;p.nested.value=22;','TS2540'],
    ['not-literal','let n=21;const p=n as const;','TS1355'],
    ['tuple-readonly','const p=[21,22] as const;p[0]=23;','TS2540'],
    ['tuple-out-of-bounds','const p=[21,22] as const;let x=p[3];','TS2493'],
    ['tuple-huge-index','const p=[21,22] as const;let x=p[2147483648];','TS2493'],
    ['tuple-constant-out-of-bounds','const p=[21,22] as const;const i=2;let x=p[i];','TS2493'],
    ['tuple-missing-contextual-slot','const p=[21,22] as const;let bad:typeof p=[];','TS2322'],
    ['tuple-negative-index','const p=[21,22] as const;let x=p[-1];','TSCS1001'],
    ['tuple-wrong-element','const p=[21,"go"] as const;let x:22=p[0];','TS2322'],
    ['tuple-to-mutable','const p=[21,22] as const;let copy:number[]=p;','TS4104'],
    ['tuple-spread-outside-scope','const p=[21,...[22]] as const;','TSCS1001'],
    ['tuple-annotation-wrong-slot','type Pair=[number,string];let p:Pair=[21,22];','TS2322'],
    ['tuple-annotation-short','type Pair=[number,string];let p:Pair=[21];','TS2322'],
    ['tuple-annotation-long','type Pair=[number,string];let p:Pair=[21,"go",true];','TS2322'],
    ['tuple-annotation-bad-write','let p:[number,string]=[21,"go"];p[0]="bad";','TS2322'],
    ['tuple-annotation-out-of-bounds','let p:[number,string]=[21,"go"];let n=p[2];','TS2493'],
    ['tuple-annotation-readonly-write','let p:readonly [number,string]=[21,"go"];p[0]=42;','TS2540'],
    ['tuple-annotation-readonly-copy','let p:readonly [number,string]=[21,"go"];let editable:[number,string]=p;','TS4104'],
    ['tuple-annotation-update-nonnumber','let p:[number,string]=[21,"go"];p[1]++;','TSCS1001'],
    ['tuple-annotation-update-out-of-bounds','let p:[number,string]=[21,"go"];p[2]++;','TS2493'],
    ['tuple-annotation-update-readonly','let p:readonly [number,string]=[21,"go"];p[0]++;','TS2540'],
    ['tuple-labeled-wrong-slot','type Pair=[first:number,second:string];let p:Pair=[21,42];','TS2322'],
    ['tuple-labeled-bad-write','type Mixed=[first:number,string];let p:Mixed=[21,"go"];p[1]=21;','TS2322'],
    ['tuple-labeled-optional-unsupported','type Bad=[first?:number];','TSCS1001'],
    ['tuple-annotation-rest-unsupported','type Bad=[...number[]];','TSCS1001'],
    ['tuple-annotation-optional-unsupported','type Bad=[number?];','TSCS1001'],
    ['fresh-extra-annotation','let x:{a:1}={a:1,b:2} as const;','TS2353'],
    ['fresh-extra-satisfies','let x=({a:1,b:2} as const) satisfies {a:1};','TS2353']
  ]) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=run(['--emitJs','--noLib','--target','es2015',join(folder,name+'.ts'),join(folder,'minimal.d.ts')]);
    // The CLI deliberately distinguishes unsupported TSCS diagnostics (exit 1)
    // from TypeScript semantic diagnostics (exit 2); neither may emit output.
    // This asserts the *existing* CLI contract instead of treating UNSUPPORTED as PASS.
    assert.equal(result.status,code.startsWith('TSCS')?1:2,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid TS must not emit JS');
  }
  console.log('type operators: four-module ES2015 result=42, label=ready, extra=42, exact=21; TS2352/TS1360/TS2353 block JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
