import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-object-keys-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export interface Slot {readonly 0:number;1:number}',
    'export const seed:Slot={0:20,1:22};',
    'export const mutable:{0:number;1:number}={0:20,1:22};'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Slot} from "./model.js";',
    'export function sum(data:Slot):number{return data[0]+data[1];}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {sum as add} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {seed,mutable} from "./model.js";',
    'import {add} from "./bridge.js";',
    'export const result:number=add(seed);',
    'mutable[0]+=1;',
    'export const before:number=mutable[0]++;',
    'export const after:number=mutable[0];',
    'export const nested:number=({child:{0:42}}).child[0];',
    'export const textKey:number=seed["0"];'
  ].join('\n'));
  function compile(name:string){
    const run=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',name,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(run.error,undefined,String(run.error));
    assert.equal(run.signal,null,'compiler crashed or timed out');
    return run;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,before,after,nested,textKey} from "./entry.js";'+
    'if(result!==42||before!==21||after!==22||nested!==42||textKey!==20)'+
    'throw Error("numeric object key runtime mismatch");'+
    'console.log(JSON.stringify({result,before,after,nested,textKey}));'
  ],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);
  const invalid=[
    ['wrong-read','let x:{0:number}={0:20};let s:string=x[0];','TS2322'],
    ['wrong-write','let x:{0:number}={0:20};x[0]="bad";','TS2322'],
    ['readonly-write','let x:{readonly 0:number}={0:20};x[0]=21;','TS2540'],
    ['readonly-update','let x:{readonly 0:number}={0:20};x[0]++;','TS2540'],
    ['wrong-initializer','let x:{0:number}={0:"bad"};','TS2322'],
    ['duplicate','const x={0:20,"0":22};','TS1117'],
    ['absent','const x={0:20};x[1];','TSCS1001'],
    ['dynamic','const x={0:20};let key:number=0;x[key];','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+'\n'+rejected.stderr);
    assert.match(rejected.stdout+'\n'+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid numeric-key program emitted JS');
  }
  console.log('Numeric object keys: four generated ES2015 modules executed; eight invalid programs emitted no JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
