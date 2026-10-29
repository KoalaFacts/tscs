import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-object-shorthand-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export const seed:number=20;',
    'export const label:string="seed";',
    'export const original={value:1};'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'export function make(value:number):{value:number}{return {value};}',
    'export function wrap(value:number,label:string):{value:number;label:string}{return {value,label};}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {make,wrap} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {seed as local,label,original} from "./model.js";',
    'import {make,wrap} from "./bridge.js";',
    'const value:number=21;',
    'export const result:number=make(value+local+1).value;',
    'export const text:string=wrap(value,label).label;',
    'export const nested:number=({inner:{value}}).inner.value;',
    'export const afterSpread:number=({...original,value}).value;',
    'export const beforeSpread:number=({value,...original}).value;',
    'export const alias:number=({local}).local;',
    'export const unchanged:number=original.value;'
  ].join('\n'));

  function compile(source:string) {
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const compiled=compile('entry.ts');
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing emitted '+name+'.js');

  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,text,nested,afterSpread,beforeSpread,alias,unchanged} from "./entry.js";',
    'if(result!==42||text!=="seed"||nested!==21||afterSpread!==21||beforeSpread!==1||alias!==20||unchanged!==1)',
    '  throw Error("object shorthand runtime mismatch");',
    'console.log(JSON.stringify({result,text,nested,afterSpread,beforeSpread,alias,unchanged}));'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);

  const invalid=[
    ['missing','let x={missing};','TS18004'],
    ['duplicate','let value=21;let x={value,value};','TS1117'],
    ['wrong-type','let value="bad";let x:{value:number}={value};','TS2322'],
    ['extra','let value=21;let x:{value:number}={value,extra:1};','TS2353'],
    ['quoted','let value=21;let x={"value"};','TSCS1001'],
    ['method','let value=21;let x={value()};','TSCS1001'],
    ['keyword','let value=21;let x={return};','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid) {
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(name+'.ts');
    assert.notEqual(result.status,0,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid object shorthand emitted JS');
  }
  console.log('Object shorthand: four-module ES2015 output executed; seven negative programs rejected.');
} finally {rmSync(folder,{recursive:true,force:true});}
