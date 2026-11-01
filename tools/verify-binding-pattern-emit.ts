import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// The emitted linked JavaScript is executed, not merely compared as text.
const dir=mkdtempSync(join(tmpdir(),'tscs-binding-patterns-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function compile(file:string) {
  const result=spawnSync(process.env.DOTNET??'dotnet',
    [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',file,'minimal.d.ts'],
    {cwd:dir,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/spread-arguments/minimal.d.ts','utf8'));
  const modules={
    'model.ts':'export const box={value:21,nested:{tag:"ok"}};export const readings:number[]=[10,20,30,40];',
    'logic.ts':'export function add(a:number,b:number):number{return a+b;}',
    'bridge.ts':'export {add} from "./logic.js";',
    'entry.ts':[
      'import {box,readings} from "./model.js";import {add} from "./bridge.js";',
      'const {value,nested:{tag}}=box;',
      'const [first,,third]=readings;',
      'let {value:mutable}:{value:number}=box;',
      'mutable=mutable+1;',
      'export const result:number=add(value,third)+first+mutable;',
      'export const label:string=tag;',
      'export function extract(input:{value:number}):number{const {value}=input;return value*2;}',
      'export const doubled:number=extract(box);',
      'let calls:number=0;',
      'function fallback():number{calls=calls+1;return 42;}',
      'const {missing=fallback(),present=fallback()}:{missing?:number,present?:number}={present:20};',
      'const [defaultFirst=fallback(),,defaultThird=fallback()]:(number|undefined)[]=[undefined,0,21];',
      'const {inner:{defaultValue=7}}:{inner:{defaultValue?:number}}={inner:{}};',
      'function makePair():{a:number,b:number}{calls=calls+1;return {a:1,b:2};}',
      'const {pair:{a:defaultA,b:defaultB}=makePair()}:{pair?:{a:number,b:number}}={};',
      'export const defaults:number=missing+present+defaultFirst+defaultThird+defaultValue+defaultA+defaultB;',
      'export const defaultCalls:number=calls;',
      'const {onlyUndefined=42}={onlyUndefined:undefined};',
      'const {optionalUndefined=undefined}:{optionalUndefined?:number}={};',
      'export const undefinedOnly:number=onlyUndefined;',
      'export const optionalUndefinedValue:number|undefined=optionalUndefined;'
    ].join('\n')
  };
  for(const [name,source] of Object.entries(modules))writeFileSync(join(dir,name),source);
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(dir,name+'.js')),'missing emitted '+name+'.js');
  const emitted=readFileSync(join(dir,'entry.js'),'utf8');
  assert.match(emitted,/__tscs_binding_/,'binding temporaries must be emitted');
  assert.doesNotMatch(emitted,/\{\s*value\s*,\s*nested\s*:/,'TypeScript binding syntax was not lowered');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,label,doubled,defaults,defaultCalls,undefinedOnly,optionalUndefinedValue} from "./entry.js";'+
    'if(result!==83||label!=="ok"||doubled!==42||defaults!==135||defaultCalls!==3||undefinedOnly!==42||optionalUndefinedValue!==undefined)throw Error(JSON.stringify({result,label,doubled,defaults,defaultCalls}));'],
    {cwd:dir,encoding:'utf8',timeout:15000,env});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);

  const negative=[
    ['missing','const {missing}={value:21};','TS2339',2],
    ['const','const {value}={value:21};value=22;','TS2588',2],
    ['array','const [value]=[21];let wrong:string=value;','TS2322',2],
    ['alias','const {value:renamed}={value:"wrong"};let wrong:number=renamed;','TS2322',2],
    ['nested','const {inner:{value}}={inner:{value:21}};let wrong:boolean=value;','TS2322',2],
    ['rest','const {...rest}={value:21};','TSCS1001',1],
    ['default-missing','const {missing=21}:{value:number}={value:21};','TS2339',2],
    ['default-mismatch','const {value=21}:{value?:number}={};let wrong:string=value;','TS2322',2],
    ['default-fallback-type','const {value="wrong"}:{value?:number}={};','TS2322',2],
    ['empty','const []=[21];','TSCS1001',1],
    ['wrong-annotation','let {value}:{value:number}={value:"bad"};','TS2322',2]
  ] as const;
  for(const [name,source,code,status] of negative) {
    writeFileSync(join(dir,'invalid-'+name+'.ts'),source);
    const bad=compile('invalid-'+name+'.ts');
    assert.equal(bad.status,status,bad.stdout+bad.stderr);
    assert.match(bad.stdout+bad.stderr,new RegExp(code));
    assert.equal(existsSync(join(dir,'invalid-'+name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('Finite object/array binding patterns: four linked ES2015 modules executed; eleven invalid programs emitted no JavaScript.');
} finally {rmSync(dir,{recursive:true,force:true});}
