import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module checked ES2015 execution plus negative no-emit, not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-division-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Payload = { value: number; rem: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Payload } from "./model.js";',
    'export function adjust(data: Payload): Payload {',
    '  data.value /= 2;',
    '  data.rem %= 5;',
    '  return data;',
    '}',
    'export function quotient(a: number, b: number): number { return a / b; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { adjust, quotient } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { adjust, quotient } from "./bridge.js";',
    'const data = { value: 84, rem: 12 };',
    'const output = adjust(data);',
    'export const result: number = output.value;',
    'export const remainder: number = output.rem;',
    'const values: number[] = [20, 5];',
    'values[0] /= 4;',
    'values[1] %= 3;',
    'export const indexed: number = values[0] + values[1];',
    'let steps: number = 0;',
    'for (let i: number = 16; i > 1; i /= 2) { steps += 1; }',
    'export const count: number = steps;',
    'export const fractional: number = quotient(11, 2) % 3;',
    'export const signed: number = -8 % 3;',
    'export const priority: number = 1 + 12 / 3 % 3;',
    'export const afterComment: number = 12 /* divisor */ / 3;'
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
    'import {result,remainder,indexed,count,fractional,signed,priority,afterComment} from "./entry.js"; if(result!==42||remainder!==2||indexed!==7||count!==4||fractional!==2.5||signed!==-2||priority!==2||afterComment!==4) throw Error("division/remainder runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  writeFileSync(join(folder,'invalid.ts'),'let invalid: string = 11 / 2;');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  console.log('numeric division/remainder: four-module ES2015 runtime result=42 remainder=2 indexed=7 count=4 fractional=2.5 signed=-2 priority=2 afterComment=4; negative TS2322 blocked emission.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
