import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Compile the same typed AST and execute emitted three-module JS in Node24.
// The fixed upstream Go differential is separate.
const folder=mkdtempSync(join(tmpdir(),'tscs-literal-types-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export type State = "ready" | "done";',
    'export interface Task { state: State; value: number }'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Task, State } from "./model.js";',
    'export function twice(input: Task): number { return input.value * 2; }',
    'export function finalState(): State { return "done"; }'
  ].join('\n'));
  writeFileSync(join(folder,'entry.ts'),[
    'import type { Task, State } from "./model.js";',
    'import { twice, finalState } from "./logic.js";',
    'const task: Task = { state: "ready", value: 21 };',
    'export const result: number = twice(task);',
    'export const state: State = finalState();'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{noLib:true,target:'es2015',module:'es2015'}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function execute(args:string[],cwd=folder){
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{
      cwd,encoding:'utf8',timeout:30000,
      env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(result.error,undefined,String(result.error));
    return result;
  }
  const compiled=execute(['--emitJs','-p','tsconfig.json']);
  assert.equal(compiled.status,0,compiled.stdout+'\n'+compiled.stderr);
  for(const name of ['model.js','logic.js','entry.js'])
    assert.ok(existsSync(join(folder,name)),name+' not emitted');
  const js=readFileSync(join(folder,'entry.js'),'utf8');
  assert.ok(!js.includes('import type')&&!js.includes(': Task')&&!js.includes(': State'));
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import { result,state } from "./entry.js"; if(result!==42 || state!=="done") throw Error("wrong literal types runtime");'
  ],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+'\n'+runtime.stderr);

  writeFileSync(join(folder,'bad.ts'),'type State = "ready" | "done"; let s: State = "invalid";');
  const invalid=execute(['--emitJs','--noLib','--target','es2015','bad.ts','minimal.d.ts']);
  assert.equal(invalid.status,2,invalid.stdout+'\n'+invalid.stderr);
  assert.match(invalid.stdout+invalid.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'bad.js')),false);
  console.log('Literal annotation, union, interface and ES module output executed: result=42 state=done; invalid literal rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
