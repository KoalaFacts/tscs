import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// A real ES2015 cross-module runtime, not just textual emitter assertions.
const folder=mkdtempSync(join(tmpdir(),'tscs-discriminant-flow-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),
    'export type Event = { kind: "ok"; value: number } | { kind: "error"; message: string };');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Event } from "./model.js";',
    'export function score(e: Event): number {',
    '  if (e.kind === "ok") { return e.value; } else { return 0; }',
    '}',
    'export function describe(e: Event): string {',
    '  if (e.kind !== "ok") { return e.message; } else { return "ready"; }',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'entry.ts'),[
    'import type { Event } from "./model.js";',
    'import { score, describe } from "./logic.js";',
    'const event: Event = { kind: "ok", value: 21 };',
    'export const result: number = score(event) + score(event);',
    'export const message: string = describe({ kind: "error", message: "bad" });'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{noLib:true,target:'es2015',module:'es2015'}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  function compile(args:string[]) {
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],{
      cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
      env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null);
    return result;
  }
  const compiled=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(compiled.status,0,compiled.stdout+'\n'+compiled.stderr);
  for(const name of ['model','logic','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),name+' was not emitted');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,message} from "./entry.js"; if(result!==42 || message!=="bad") throw Error("incorrect narrowed output");'
  ],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);

  writeFileSync(join(folder,'bad.ts'),
    'type Event = { kind: "ok"; value: number } | { kind: "error"; message: string }; '+
    'function wrong(e: Event): number { if (e.kind === "ok") { return e.message; } else { return 0; } }');
  const rejected=compile(['--emitJs','--noLib','--target','es2015','bad.ts','minimal.d.ts']);
  assert.equal(rejected.status,2,rejected.stdout+'\n'+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2339/);
  assert.equal(existsSync(join(folder,'bad.js')),false);
  console.log('Discriminated union checking + three-module emitted JS executed: result=42, message=bad; invalid branch rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
