import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-for-in-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export const record={first:20,second:22};',
    'export const numbers:number[]=[20,22];'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'export function objectKeys(record:{first:number;second:number}):number {',
    ' let total:number=0;',
    ' for(const key in record) {',
    '   if(key==="first"){total+=20;continue;}',
    '   if(key==="second"){total+=22;}',
    ' } return total;',
    '}',
    'export function arrayKeys(numbers:number[]):number {',
    ' let total:number=0;',
    ' for(let index in numbers) {',
    '   if(index==="0"){total+=20;}',
    '   if(index==="1"){total+=22;break;}',
    ' } return total;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {objectKeys,arrayKeys} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {record,numbers} from "./model.js";',
    'import {objectKeys,arrayKeys} from "./bridge.js";',
    'export const result:number=objectKeys(record);',
    'export const arrayResult:number=arrayKeys(numbers);',
    'let outer:string="outer";',
    'let count:number=0;',
    'for(let outer in record) {for(const inner in numbers){if(outer==="first" && inner==="0"){count+=42;}}}',
    'export const nested:number=count;',
    'export const retained:string=outer;',
    'let zero:number=0;for(const key in {}){zero++;}',
    'export const empty:number=zero;'
  ].join('\n'));
  function compile(name:string) {
    const result=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',name,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(result.error,undefined,String(result.error));
    assert.equal(result.signal,null,'compiler crashed or timed out');
    return result;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+'\n'+built.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,arrayResult,nested,retained,empty} from "./entry.js";'+
    'if(result!==42 || arrayResult!==42 || nested!==42 || retained!=="outer" || empty!==0)'+
    'throw Error("wrong checked for-in runtime: "+JSON.stringify({result,arrayResult,nested,retained,empty}));'+
    'console.log(JSON.stringify({result,arrayResult,nested,retained,empty}));'
  ],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+'\n'+executed.stderr);
  // The former unbraced-unsupported input is a real positive now, not a
  // deleted regression or an invalid-program expectation.
  writeFileSync(join(folder,'unbraced.ts'),'for(const key in {a:1})key;');
  const unbraced=compile('unbraced.ts');
  assert.equal(unbraced.status,0,unbraced.stdout+'\n'+unbraced.stderr);
  assert.ok(existsSync(join(folder,'unbraced.js')),'unbraced for..in emitted no JS');
  const unbracedRun=spawnSync(process.execPath,['--input-type=module','--eval',
    'import "./unbraced.js";'
  ],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(unbracedRun.error,undefined,String(unbracedRun.error));
  assert.equal(unbracedRun.status,0,unbracedRun.stdout+'\n'+unbracedRun.stderr);
  const invalid=[
    ['number-source','for(const key in 42){}','TS2407'],
    ['boolean-source','for(let key in true){}','TS2407'],
    ['wrong-key-type','for(const key in {a:1}){let n:number=key;}','TS2322'],
    ['wrong-body-type','for(const key in {a:1}){let s:string=1;}','TS2322'],
    ['outside-scope','for(const key in {a:1}){}let s:string=key;','TS2304'],
    ['unbraced-lexical','for(const key in {a:1})const value=1;','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=compile(name+'.ts');
    assert.notEqual(rejected.status,0,rejected.stdout+'\n'+rejected.stderr);
    assert.match(rejected.stdout+'\n'+rejected.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid for-in emitted JS: '+name);
  }
  console.log('Checked for-in: four ES2015 modules executed; six invalid programs emitted no JS.');
} finally { rmSync(folder,{recursive:true,force:true}); }
