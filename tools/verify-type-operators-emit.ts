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
  writeFileSync(join(folder,'model.ts'),'export type Item = { value: number; label: string };\n');
  writeFileSync(join(folder,'factory.ts'),[
    'import type { Item } from "./model.js";',
    'export function fromAny(value: any): Item {',
    '  return (value as Item) satisfies Item;',
    '}',
    'export function make(value: number): Item {',
    '  const item = { value: value, label: "ok" } satisfies Item;',
    '  return item;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { fromAny, make } from "./factory.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { fromAny, make } from "./bridge.js";',
    'const raw: any = { value: 21, label: "ready" };',
    'const item = fromAny(raw);',
    'export const result: number = make(item.value).value * 2;',
    'export const label: string = item.label;',
    'export const extra: number = ({ value: 42, extra: 3 } as { value: number }).value;',
    'export const exact: 21 = (21 as 21);',
    'export const checked: string = "ready" satisfies string;',
    'export const advanced: number = ((21 as number) satisfies number) + 21;'
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
    'import {result,label,extra,exact,checked,advanced} from "./entry.js"; if(result!==42||label!=="ready"||extra!==42||exact!==21||checked!=="ready"||advanced!==42) throw Error("type-operator JS runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  for(const [name,source,code] of [
    ['invalid-assert','let value: number = 21; let s = value as string;','TS2352'],
    ['invalid-satisfies','const value = "wrong" satisfies number;','TS1360'],
    ['invalid-fresh','const bad = {value: 21, excess: 2} satisfies {value: number};','TS2353']
  ]) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=run(['--emitJs','--noLib','--target','es2015',join(folder,name+'.ts'),join(folder,'minimal.d.ts')]);
    assert.equal(result.status,2,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid TS must not emit JS');
  }
  console.log('type operators: four-module ES2015 result=42, label=ready, extra=42, exact=21; TS2352/TS1360/TS2353 block JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
