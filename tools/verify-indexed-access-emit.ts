import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {runTscs} from './tscs-process.ts';

const dir=mkdtempSync(join(tmpdir(),'tscs-indexed-access-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(dir,'model.ts'),[
    'export interface Bag{value:number;label:string;nested:{amount:number}}',
    'export type Value=Bag["value"];',
    'export type Label=Bag["label"];',
    'export type Amount=Bag["nested"]["amount"];',
    'export type Either=Bag["value"|"label"];',
    'export type All=Bag[keyof Bag];'
  ].join('\n'));
  writeFileSync(join(dir,'logic.ts'),[
    'import type {Value,Label,Amount,Either} from "./model.js";',
    'export function twice(x:Value):Value{return x*2;}',
    'export function next(x:Amount):Amount{return x+1;}',
    'export function echo(x:Label):Label{return x;}',
    'export function passed(x:Either):Either{return x;}'
  ].join('\n'));
  writeFileSync(join(dir,'bridge.ts'),'export {twice,next,echo,passed} from "./logic.js";\n');
  writeFileSync(join(dir,'entry.ts'),[
    'import type {Bag,Value,Label,Either} from "./model.js";',
    'import {twice,next,echo,passed} from "./bridge.js";',
    'const bag:Bag={value:21,label:"ready",nested:{amount:41}};',
    'export const result:Value=twice(bag.value);',
    'export const nested:number=next(bag.nested.amount);',
    'export const label:Label=echo(bag.label);',
    'export const union:Either=passed("hello");'
  ].join('\n'));
  writeFileSync(join(dir,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
      function compile(args:string[]){return runTscs(dir,args);}
  const good=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(good.status,0,good.stdout+good.stderr);
  for(const f of ['model','logic','bridge','entry'])assert.ok(existsSync(join(dir,f+'.js')));
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,nested,label,union} from "./entry.js"; if(result!==42||nested!==42||label!=="ready"||union!=="hello")throw Error("indexed access runtime mismatch");'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const invalid:[string,string,string,number][]=[
    ['mismatch','type V={x:number}["x"];let n:V="bad";','TS2322',2],
    ['missing','type V={x:number}["bad"];let n:V=42;','TS2339',2],
    ['partial','type V={x:number}["x"|"bad"];let n:V=42;','TS2339',2],
    ['unbounded','type V={x:number}[string];let n:V=42;','TSCS1001',1],
    ['primitive','type V=number["x"];let n:V=42;','TSCS1001',1],
    ['unbound','type V={x:number}[Missing];let n:V=42;','TS2304',2],
    ['union','type V=({x:number}|{y:string})["x"];let n:V=42;','TS2339',2]
  ];
  invalid.push(
    ['nested-numeric',"type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(A|B)[\"nested\"];let k:keyof V=0;",'TS2322',2],
    ['nested-string',"type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(A|B)[\"nested\"];let k:keyof V=\"0\";",'TS2322',2],
    ['nested-reversed-numeric',"type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(B|A)[\"nested\"];let k:keyof V=0;",'TS2322',2],
    ['nested-reversed-string',"type A={nested:{0:number}};type B={nested:{\"0\":number}};type V=(B|A)[\"nested\"];let k:keyof V=\"0\";",'TS2322',2]
  );
  for(const [name,code,diagnostic,exit] of invalid) {
    const file=join(dir,'bad-'+name+'.ts');
    writeFileSync(file,code);
    const rejected=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'failed source emitted JavaScript');
  }
  console.log('indexed access: four-module ES2015 result/nested=42; seven negative programs rejected.');
} finally {rmSync(dir,{recursive:true,force:true});}
