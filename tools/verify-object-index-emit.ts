import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-object-index-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export const seed={value:21,label:"ok"};',
    'export const store:{value:number}={value:20};'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),
    'export function doubled(data:{value:number}):number{return data["value"]*2;}\n');
  writeFileSync(join(folder,'bridge.ts'),'export {doubled} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {seed,store} from "./model.js";',
    'import {doubled} from "./bridge.js";',
    'export const result:number=doubled(seed);',
    'store["value"]+=1;',
    'export const before:number=store["value"]++;',
    'export const after:number=store["value"];',
    'export const nested:number=({inner:{value:42}})["inner"]["value"];',
    'export const special:number=({"data-key":42})["data-key"];',
    'export const label:string=seed["label"];'
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
    assert.ok(existsSync(join(folder,name+'.js')),'missing emitted '+name+'.js');
  const execution=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,before,after,nested,special,label} from "./entry.js";',
    'if(result!==42||before!==21||after!==22||nested!==42||special!==42||label!=="ok")',
    '  throw Error("object literal bracket access runtime mismatch");',
    'console.log(JSON.stringify({result,before,after,nested,special,label}));'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(execution.error,undefined,String(execution.error));
  assert.equal(execution.status,0,execution.stdout+execution.stderr);
  const invalid=[
    ['readonly','let x:{readonly value:number}={value:21};x["value"]=42;','TS2540'],
    ['readonly-compound','let x:{readonly value:number}={value:21};x["value"]+=1;','TS2540'],
    ['readonly-update','let x:{readonly value:number}={value:21};x["value"]++;','TS2540'],
    ['wrong-write','let x:{value:number}={value:21};x["value"]="bad";','TS2322'],
    ['wrong-read','let x={value:21};let s:string=x["value"];','TS2322'],
    ['missing','let x={value:21};x["missing"];','TS2339'],
    ['dynamic','let x={value:21};let key:string="value";x[key];','TSCS1001'],
    ['object-number','let x={value:21};x[0];','TSCS1001'],
    ['array-string','let xs=[1,2];xs["0"];','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid index program emitted JS');
  }
  console.log('String-literal object indexes: four real ES2015 modules executed; nine invalid programs rejected.');
} finally {rmSync(folder,{recursive:true,force:true});}
