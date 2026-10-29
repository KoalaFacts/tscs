import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module checked ES2015 execution, plus no-emit negative probes.
const folder=mkdtempSync(join(tmpdir(),'tscs-keyof-object-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export interface Data { value: number; label: string }',
    'export type Field = keyof Data;',
    'export interface Left { value: number; left: string }',
    'export interface Right { value: number; right: string }',
    'export type Shared = keyof (Left | Right);'
  ].join('\n'));
  writeFileSync(join(folder,'factory.ts'),[
    'import type { Field, Shared } from "./model.js";',
    'export function echo(field: Field): Field { return field; }',
    'export function score(field: Field): number { return field === "value" ? 42 : 0; }',
    'export function common(field: Shared): Shared { return field; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { echo, score, common } from "./factory.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import type { Data, Field, Shared } from "./model.js";',
    'import { echo, score, common } from "./bridge.js";',
    'const data: Data = {value: 42, label: "ready"};',
    'const selected: Field = "value";',
    'export const result: number = score(echo(selected));',
    'export const label: string = data.label;',
    'export const key: string = echo("label");',
    'export const name: Field = echo("value");',
    'export const shared: Shared = common("value");'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const r=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const compiled=compile(['--emitJs','-p',join(folder,'project.json')]);
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','factory','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,key,name,shared} from "./entry.js"; if(result!==42||label!=="ready"||key!=="label"||name!=="value"||shared!=="value") throw Error("keyof runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const failures:[string,string,string,number][]=[
    ['missing','type K = keyof {a:number}; let x: K = "bad";','TS2322',2],
    ['numeric','type K = keyof {a:number}; let x: K = 2;','TS2322',2],
    ['boolean','type K = keyof {a:number}; let x: K = true;','TS2322',2],
    ['empty','type K = keyof {}; let x: K = "bad";','TS2322',2],
    ['primitive','type K = keyof number; let x: K = "toString";','TSCS1001',1],
    ['numeric-key','type K = keyof {0:number}; let x: K = 0;','TSCS1001',1],
    ['union-left','type A={shared:number;left:number}; type B={shared:number;right:number}; let x:keyof (A|B)="left";','TS2322',2],
    ['union-empty','type A={a:number}; type B={b:number}; let x:keyof (A|B)="a";','TS2322',2],
    ['union-primitive','type A={a:number}; let x:keyof (A|number)="a";','TSCS1001',1]
  ];
  for(const [name,content,code,exit] of failures) {
    const file=join(folder,'bad-'+name+'.ts');
    writeFileSync(file,content);
    const bad=compile(['--emitJs','--noLib','--target','es2015',file,join(folder,'minimal.d.ts')]);
    assert.equal(bad.status,exit,bad.stdout+bad.stderr);
    assert.match(bad.stdout+bad.stderr,new RegExp(code));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'invalid source emitted JS: '+name);
  }
  console.log('keyof object: four-module ES2015 result=42, nine invalid programs fail closed.');
} finally {rmSync(folder,{recursive:true,force:true});}
