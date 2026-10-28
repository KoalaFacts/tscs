import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Real multi-module output execution, with negative TS2322 no-emit check.
const folder=mkdtempSync(join(tmpdir(),'tscs-string-addition-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item = { name: string; code: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Item } from "./model.js";',
    'export function decorate(item: Item): string {',
    '  item.name += "-" + item.code;',
    '  return item.name + "-ready=" + true;',
    '}',
    'export function increment(n: number): number { return n + 1; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { decorate, increment } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { decorate, increment } from "./bridge.js";',
    'const item = { name: "job", code: 21 };',
    'export const result: string = decorate(item);',
    'export const stored: string = item.name;',
    'export const total: number = increment(41);',
    'let suffix: string = "x"; suffix += 2; suffix += false;',
    'export const chained: string = suffix;',
    'const numbers: number[] = [2, 8];',
    'export const indexed: string = "index=" + numbers[1];',
    'export const reversed: string = 21 + " items";',
    'const dynamic: any = 42;',
    'export const withAny: string = "value=" + dynamic;',
    'export const booleanPrefix: string = false + "-ready";'
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
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,stored,total,chained,indexed,reversed,withAny,booleanPrefix} from "./entry.js"; if(result!=="job-21-ready=true"||stored!=="job-21"||total!==42||chained!=="x2false"||indexed!=="index=8"||reversed!=="21 items"||withAny!=="value=42"||booleanPrefix!=="false-ready") throw Error("string addition runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  writeFileSync(join(folder,'invalid.ts'),'let n: number = 21; n += "not numeric";');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  console.log('string addition: four-module ES2015 runtime result=job-21-ready=true total=42 chained=x2false; TS2322 blocked JS emit.');
} finally {rmSync(folder,{recursive:true,force:true});}
