import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {runTscs} from './tscs-process.ts';

// A real ES2015 cross-module runtime, not just textual emitter assertions.
const folder=mkdtempSync(join(tmpdir(),'tscs-guard-flow-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),
    'export type Event = { kind: "ok"; value: number } | { kind: "error"; message: string };');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Event } from "./model.js";',
    'export function score(e: Event): number {',
    '  if (e.kind !== "ok") { return 0; }',
    '  return e.value;',
    '}',
    'export function describe(e: Event): string {',
    '  if (e.kind === "ok") { return "ready"; }',
    '  return e.message;',
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
  function compile(args:string[]){return runTscs(folder,args);}
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
    'function wrong(e: Event): string { if (e.kind !== "ok") { return "bad"; } return e.message; }');
  const rejected=compile(['--emitJs','--noLib','--target','es2015','bad.ts','minimal.d.ts']);
  assert.equal(rejected.status,2,rejected.stdout+'\n'+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2339/);
  assert.equal(existsSync(join(folder,'bad.js')),false);
  console.log('Returning guard flow + three-module emitted JS executed: result=42, message=bad; invalid branch rejected.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
