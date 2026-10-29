import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-in-operator-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export const seed={value:21,label:"ok"};',
    'export const values:number[]=[20,22];'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),
    'export function hasValue(data:{value:number}):boolean{return "value" in data;}\n');
  writeFileSync(join(folder,'bridge.ts'),'export {hasValue} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {seed,values} from "./model.js";',
    'import {hasValue} from "./bridge.js";',
    'export const present:boolean=hasValue(seed);',
    'export const missing:boolean="absent" in seed;',
    'let key:string="label";',
    'export const dynamicKey:boolean=key in seed;',
    'export const arrayIndex:boolean=1 in values;',
    'export const arrayMissing:boolean=2 in values;',
    'export const keyword:boolean="in" in {in:42};',
    'export const combined:boolean=("value" in seed) && ("label" in seed);',
    'export const result:number=(present && dynamicKey && arrayIndex && keyword && combined && !missing && !arrayMissing)?42:0;'
  ].join('\n'));
  function compile(name:string) {
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',name,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const compiled=compile('entry.ts');
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const execution=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {present,missing,dynamicKey,arrayIndex,arrayMissing,keyword,combined,result} from "./entry.js";',
    'if(!present || missing || !dynamicKey || !arrayIndex || arrayMissing || !keyword || !combined || result!==42)',
    '  throw Error("in-operator emitted runtime mismatch");',
    'console.log(JSON.stringify({present,missing,dynamicKey,arrayIndex,arrayMissing,keyword,combined,result}));'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(execution.error,undefined,String(execution.error));
  assert.equal(execution.status,0,execution.stdout+execution.stderr);
  const invalid=[
    ['boolean-left','let obj={value:42};let result:boolean=true in obj;','TS2360'],
    ['number-right','let x:number=42;let result:boolean="value" in x;','TS2361'],
    ['wrong-result','let obj={value:42};let result:number="value" in obj;','TS2322'],
    ['boolean-right','let x:boolean=true;let result:boolean="value" in x;','TS2361'],
    ['object-left','let obj={value:42};let result:boolean=({value:1}) in obj;','TS2360']
  ] as const;
  for(const [name,source,diagnostic] of invalid) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid in expression emitted JS');
  }
  console.log('Checked in-operator: four ES2015 modules executed; five invalid programs rejected.');
} finally {rmSync(folder,{recursive:true,force:true});}
