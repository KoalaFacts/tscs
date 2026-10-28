import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module runtime and negative no-emit validation for checked readonly object members.
const folder=mkdtempSync(join(tmpdir(),'tscs-readonly-'));
const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function check(args:string[]){
  const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try{
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export interface Reading { readonly value: number; label: string }',
    'export type Box = { readonly item: Reading };'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Reading } from "./model.js";',
    'export function twice(reading: Reading): number { return reading.value * 2; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { twice } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import type { Reading, Box } from "./model.js";',
    'import { twice } from "./bridge.js";',
    'let reading: Reading = { value: 21, label: "seed" };',
    'const box: Box = { item: reading };',
    'reading.label = "ready";',
    'const named: { readonly: number } = { readonly: 2 };',
    'named.readonly = 3;',
    'export const result: number = twice(box.item);',
    'export const label: string = reading.label;',
    'export const namedResult: number = named.readonly;'
  ].join('\n'));
  const built=check(['--emitJs','--noLib','--target','es2015','--module','es2015','entry.ts','minimal.d.ts']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  for(const name of ['model','logic','bridge','entry']){
    const js=readFileSync(join(folder,name+'.js'),'utf8');
    assert.doesNotMatch(js,/\b(readonly value|readonly item|interface Reading|type Box|import type)\b/);
  }
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,namedResult} from "./entry.js"; if(result!==42||label!=="ready"||namedResult!==3) throw Error("readonly graph mismatch"); console.log(JSON.stringify({result,label,namedResult}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  assert.deepEqual(JSON.parse(runtime.stdout),{result:42,label:'ready',namedResult:3});
  for(const [name,source] of [
    ['invalid-write','import type { Reading } from "./model.js"; let item: Reading = {value: 21,label:"ok"}; item.value = 42;'],
    ['invalid-compound','import type { Reading } from "./model.js"; let item: Reading = {value: 21,label:"ok"}; item.value += 1;'],
    ['invalid-nested','import type { Box } from "./model.js"; let box: Box = {item:{value:21,label:"ok"}}; box.item = {value:42,label:"bad"};'],
    ['invalid-deep','import type { Box } from "./model.js"; let box: Box = {item:{value:21,label:"ok"}}; box.item.value = 42;']
  ]){
    writeFileSync(join(folder,name+'.ts'),source);
    const invalid=check(['--emitJs','--noLib','--target','es2015','--module','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(invalid.status,2,invalid.stdout+invalid.stderr);
    assert.match(invalid.stdout+invalid.stderr,/TS2540/);
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid readonly write must not emit JS');
  }
  console.log('readonly: four-module ES2015 result=42 label=ready namedResult=3; four TS2540 negatives block all JS emission.');
}finally{rmSync(folder,{recursive:true,force:true});}
