import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Bounded actual ES2015 execution; original TS7 acceptance credit is zero.
const folder=mkdtempSync(join(tmpdir(),'tscs-prefix-updates-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'data.ts'),'export const values:number[]=[2,20,19];\n');
  writeFileSync(join(folder,'logic.ts'),[
    'export function compute(values:number[]):number {',
    '  let i:number=-1;',
    '  let a:number=++values[++i];',
    '  let b:number=++values[++i];',
    '  let box:{value:number}={value:17};',
    '  let c:number=++box.value;',
    '  return a+b+c;',
    '}',
    'export function count():number {',
    '  let sum:number=0;',
    '  for(let i:number=0;i<3;++i){ ++sum; }',
    '  return sum;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { compute as calculate, count } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { values } from "./data.js";',
    'import { calculate, count } from "./bridge.js";',
    'export const result:number=calculate(values);',
    'export const first:number=values[0];',
    'export const second:number=values[1];',
    'export const iterations:number=count();'
  ].join('\n'));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(file:string){
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015','--module','es2015',file,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler timed out or crashed');
    return result;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['data','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,first,second,iterations} from "./entry.js"; console.log(JSON.stringify({result,first,second,iterations}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  assert.deepEqual(JSON.parse(run.stdout),{result:42,first:3,second:21,iterations:3});
  const negatives=[
    ['const-identifier','const n:number=1; ++n;','TS2588'],
    ['readonly-property','interface Item {readonly value:number} let item:Item={value:1}; ++item.value;','TS2540'],
    ['non-number','let s:string="x"; ++s;','TSCS1001'],
    ['string-index','let s:string="ab"; ++s[0];','TS2542'],
    ['invalid-target','let n:number=1; ++(n+1);','TSCS1001']
  ] as const;
  for(const [name,source,diagnostic] of negatives){
    writeFileSync(join(folder,name+'.ts'),source);
    const bad=compile(name+'.ts');
    assert.notEqual(bad.status,0,bad.stdout+bad.stderr);
    assert.match(bad.stdout+bad.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted '+name);
  }
  console.log('Prefix numeric updates: four-module ES2015 runtime result=42, first=3, second=21, iterations=3; five negative programs block emission.');
} finally {rmSync(folder,{recursive:true,force:true});}
