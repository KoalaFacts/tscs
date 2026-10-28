import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual C# compilation and executed ES2015 modules; no text-only acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-switch-flow-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),
    'export type Event = { kind: "ok"; value: number } | { kind: "error"; message: string };');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Event } from "./model.js";',
    'export function score(e: Event): number {',
    '  switch (e.kind) {',
    '    case "ok": { return e.value; }',
    '    case "error": { return 0; }',
    '    default: { return 0; }',
    '  }',
    '}',
    'export function describe(e: Event): string {',
    '  switch (e.kind) {',
    '    case "ok": { return "ready"; }',
    '    default: { return e.message; }',
    '  }',
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
    'import {result,message} from "./entry.js"; if(result!==42 || message!=="bad") throw Error("incorrect switch output");'
  ],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);
  writeFileSync(join(folder,'bad.ts'),
    'type E = {kind:"ok";value:number}|{kind:"error";message:string}; '+
    'function wrong(e:E):number { switch(e.kind) { case "error": { return e.value; } default: { return 0; } } }');
  const rejected=compile(['--emitJs','--noLib','--target','es2015','bad.ts','minimal.d.ts']);
  assert.equal(rejected.status,2,rejected.stdout+'\n'+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2339/);
  assert.equal(existsSync(join(folder,'bad.js')),false);
  console.log('Discriminant switch arms emitted and executed: result=42, message=bad; invalid branch rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
