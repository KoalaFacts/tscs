import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const dir=mkdtempSync(join(tmpdir(),'tscs-object-intersection-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(dir,'model.ts'),[
    'export interface Base { amount: number }',
    'export type Label = { label: string };',
    'export type Pair = Base & Label;',
    'export type Amount = Pair["amount"];',
    'export type Field = keyof Pair;'
  ].join('\n'));
  writeFileSync(join(dir,'logic.ts'),[
    'import type {Pair,Amount,Field} from "./model.js";',
    'export function next(p:Pair):Amount {return p.amount + 22;}',
    'export function title(p:Pair):string {return p.label;}',
    'export function choose(k:Field):Field {return k;}'
  ].join('\n'));
  writeFileSync(join(dir,'bridge.ts'),'export {next,title,choose} from "./logic.js";\n');
  writeFileSync(join(dir,'entry.ts'),[
    'import type {Pair,Field} from "./model.js";',
    'import {next,title,choose} from "./bridge.js";',
    'const value:Pair={amount:20,label:"ready"};',
    'export const result:number=next(value);',
    'export const text:string=title(value);',
    'export const key:Field=choose("label");'
  ].join('\n'));
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(args:string[]) {
    const result=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
      {cwd:dir,encoding:'utf8',timeout:30000,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const f of ['model','logic','bridge','entry'])assert.ok(existsSync(join(dir,f+'.js')));
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,text,key} from "./entry.js"; if(result!==42||text!=="ready"||key!=="label")throw Error("intersection runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const invalid:[string,string,string,number][]=[
    ['wrong-type','type P={a:number}&{b:string};let x:P={a:1,b:"ok"};let n:number=x.b;','TS2322',2],
    ['missing','type P={a:number}&{b:string};let x:P={a:1,b:"ok"};let n:number=x.missing;','TS2339',2],
    ['readonly','type P={readonly a:number}&{b:string};let x:P={a:1,b:"ok"};x.a=2;','TS2540',2],
    ['keyof','type P={a:number}&{b:string};let k:keyof P="missing";','TS2322',2],
    ['primitive','type P={a:number}&number;let x:P;','TSCS1001',1],
    ['conflict','type P={a:number}&{a:string};let x:P;','TSCS1001',1],
    ['numeric-identity','type P={0:number}&{"0":number};let x:P;','TSCS1001',1],
    ['union-operand','type P=({a:number}|{b:string})&{c:boolean};let x:P;','TSCS1001',1]
  ];
  for(const [name,code,diagnostic,exit] of invalid) {
    const file=join(dir,'bad-'+name+'.ts');
    writeFileSync(file,code);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(file.replace(/\\.ts$/,'.js')),false,'failed source emitted JavaScript');
  }
  console.log('finite object intersection: four-module ES2015 result=42; eight negative programs rejected.');
} finally {rmSync(dir,{recursive:true,force:true});}
