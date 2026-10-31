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
    'export const values:number[]=[20,22];',
    'export const field="score";'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),
    'import {field} from "./model.js";\n'+
    'export function hasValue(data:{value:number}):boolean{return "value" in data;}\n'+
    'export function read(data:{score:number}|{reason:string}):number{if("score" in data){return data.score;}return 0;}\n'+
    'export function explain(data:{score:number}|{reason:string}):string{if("score" in data){return "present";}return data.reason;}\n'+
    'export function optionalRead(data:{value?:number}|{message:string}):number{if("value" in data){return data.value??0;}return 0;}\n'+
    'export function optionalInverse(data:{value?:number}|{message:string}):number{if(!("value" in data)){return 0;}return data.value??0;}\n'+
    'export function constKey(data:{score:number}|{reason:string}):number{const key="score";if(key in data){return data.score;}return 0;}\n'+
    'export function numericKey(data:{0:number}|{reason:string}):number{const key=0;if(key in data){return data[0];}return 0;}\n'+
    'export function importedKey(data:{score:number}|{reason:string}):number{if(field in data){return data.score;}return 0;}\n'+
    'export function typedKey(data:{score:number}|{reason:string},key:"score"):number{if(key in data){return data.score;}return 0;}\n');
  writeFileSync(join(folder,'bridge.ts'),'export {hasValue,read,explain,optionalRead,optionalInverse,constKey,numericKey,importedKey,typedKey} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {seed,values} from "./model.js";',
    'import {hasValue,read,explain,optionalRead,optionalInverse,constKey,numericKey,importedKey,typedKey} from "./bridge.js";',
    'export const present:boolean=hasValue(seed);',
    'export const narrowedHit:number=read({score:42});',
    'export const narrowedMiss:number=read({reason:"not-found"});',
    'export const narrowedFalse:string=explain({reason:"missing"});',
    'export const optionalHit:number=optionalRead({value:42});',
    'export const optionalEmpty:number=optionalRead({});',
    'export const optionalMiss:number=optionalRead({message:"missing"});',
    'export const optionalInverseHit:number=optionalInverse({value:20});',
    'export const constKeyHit:number=constKey({score:42});',
    'export const constKeyMiss:number=constKey({reason:"missing"});',
    'export const numericKeyHit:number=numericKey({0:77});',
    'export const numericKeyMiss:number=numericKey({reason:"missing"});',
    'export const importedKeyHit:number=importedKey({score:41});',
    'export const importedKeyMiss:number=importedKey({reason:"missing"});',
    'export const typedKeyHit:number=typedKey({score:32},"score");',
    'export const missing:boolean="absent" in seed;',
    'let key:string="label";',
    'export const dynamicKey:boolean=key in seed;',
    'export const arrayIndex:boolean=1 in values;',
    'export const arrayMissing:boolean=2 in values;',
    'export const keyword:boolean="in" in {in:42};',
    'export const combined:boolean=("value" in seed) && ("label" in seed);',
    'export const callable:boolean="prototype" in hasValue;',
    'let observed:boolean=false;',
    'for(let found:boolean=("value" in seed);found;){observed=true;break;}',
    'export const groupedFor:boolean=observed;',
    'function keep(x:boolean):boolean{return x;}',
    'let callObserved:boolean=false;',
    'for(let accepted:boolean=keep("value" in seed);accepted;){callObserved=true;break;}',
    'export const callFor:boolean=callObserved;',
    'let arrayObserved:boolean=false;',
    'for(let flags=["value" in seed];flags[0];){arrayObserved=true;break;}',
    'export const arrayFor:boolean=arrayObserved;',
    'let objectObserved:boolean=false;',
    'for(let snapshot={present:"value" in seed};snapshot.present;){objectObserved=true;break;}',
    'export const objectFor:boolean=objectObserved;',
    'let conditionalObserved:boolean=false;',
    'for(let accepted=true?"value" in seed:false;accepted;){conditionalObserved=true;break;}',
    'export const conditionalFor:boolean=conditionalObserved;',
    'export const result:number=(present && dynamicKey && arrayIndex && keyword && combined && callable && groupedFor && callFor && arrayFor && objectFor && conditionalFor && !missing && !arrayMissing)?42:0;'
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
    'import {present,missing,dynamicKey,arrayIndex,arrayMissing,keyword,combined,callable,groupedFor,callFor,arrayFor,objectFor,conditionalFor,result,narrowedHit,narrowedMiss,narrowedFalse,optionalHit,optionalEmpty,optionalMiss,optionalInverseHit,constKeyHit,constKeyMiss,numericKeyHit,numericKeyMiss,importedKeyHit,importedKeyMiss,typedKeyHit} from "./entry.js";',
    'if(!present || missing || !dynamicKey || !arrayIndex || arrayMissing || !keyword || !combined || !callable || !groupedFor || !callFor || !arrayFor || !objectFor || !conditionalFor || result!==42 || narrowedHit!==42 || narrowedMiss!==0 || narrowedFalse!=="missing" || optionalHit!==42 || optionalEmpty!==0 || optionalMiss!==0 || optionalInverseHit!==20 || constKeyHit!==42 || constKeyMiss!==0 || numericKeyHit!==77 || numericKeyMiss!==0 || importedKeyHit!==41 || importedKeyMiss!==0 || typedKeyHit!==32)',
    '  throw Error("in-operator emitted runtime mismatch");',
    'console.log(JSON.stringify({present,missing,dynamicKey,arrayIndex,arrayMissing,keyword,combined,result}));'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(execution.error,undefined,String(execution.error));
  assert.equal(execution.status,0,execution.stdout+execution.stderr);
  const invalid=[
    ['boolean-left','let obj={value:42};let result:boolean=true in obj;','TS2322'],
    ['union-unguarded','type E={score:number}|{reason:string};function bad(e:E):number{return e.score;}','TS2339'],
    ['union-wrong-arm','type E={score:number}|{reason:string};function bad(e:E):string{if("score" in e){return e.reason;}return "ok";}','TS2339'],
    ['dynamic-key-does-not-narrow','type E={score:number}|{reason:string};function bad(e:E,key:string):number{if(key in e){return e.score;}return 0;}','TS2339'],
    ['explicitly-widened-const-key','type E={score:number}|{reason:string};function bad(e:E):number{const key:string="score";if(key in e){return e.score;}return 0;}','TS2339'],
    ['mutable-inferred-key','type E={score:number}|{reason:string};function bad(e:E):number{let key="score";if(key in e){return e.score;}return 0;}','TS2339'],
    ['union-stale-after-write','type E={score:number}|{reason:string};function bad(e:E):number{if("score" in e){e={reason:"new"};return e.score;}return 0;}','TS2339'],
    ['number-right','let x:number=42;let result:boolean="value" in x;','TS2322'],
    ['wrong-result','let obj={value:42};let result:number="value" in obj;','TS2322'],
    ['boolean-right','let x:boolean=true;let result:boolean="value" in x;','TS2322'],
    ['object-left','let obj={value:42};let result:boolean=({value:1}) in obj;','TS2322'],
    ['bare-in-for-initializer','let obj={value:42};for(let found:boolean="value" in obj;false;){}','TSCS1001'],
    ['bare-in-conditional-false','let obj={value:42};for(let found=false?false:"value" in obj;false;){}','TSCS1001']
  ] as const;
  for(const [name,source,diagnostic] of invalid) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid in expression emitted JS');
  }
  console.log('Checked in-operator: four ES2015 modules executed, finite union branches checked; thirteen invalid programs rejected.');
} finally {rmSync(folder,{recursive:true,force:true});}
