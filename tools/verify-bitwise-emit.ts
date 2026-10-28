import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module checked ES2015 execution and negative no-emit; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-bitwise-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Packet = { left: number; right: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Packet } from "./model.js";',
    'export function combine(data: Packet): number {',
    '  return ((data.left & 15) << 2) | (data.right ^ 7);',
    '}',
    'export function mask(n: number): number { return ~n & 255; }',
    'export function shift(n: number): number { return n >>> 1; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { combine, mask, shift } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { combine, mask, shift } from "./bridge.js";',
    'export const result: number = combine({ left: 10, right: 5 });',
    'export const masked: number = mask(0);',
    'export const shifted: number = shift(-2);',
    'export const priority: number = 1 | 2 & 4;',
    'export const addFirst: number = 1 << 3 + 2;'
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
    'import {result,masked,shifted,priority,addFirst} from "./entry.js"; if(result!==42||masked!==255||shifted!==2147483647||priority!==1||addFirst!==32) throw Error("numeric bitwise runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  writeFileSync(join(folder,'invalid.ts'),'let invalid: string = 3 & 2;');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  console.log('numeric bitwise: four-module ES2015 runtime result=42 masked=255 shifted=2147483647 priority=1 addFirst=32; invalid assignment refused without output.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
