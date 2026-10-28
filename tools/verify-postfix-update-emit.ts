import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module checked ES2015 output; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-postfix-updates-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'data.ts'),'export const values:number[]=[20,21];\n');
  writeFileSync(join(folder,'logic.ts'),[
    'export function compute(values:number[]):number {',
    '  let i:number=0;',
    '  let first:number=values[i++]++;',
    '  let second:number=values[i++]++;',
    '  let box:{value:number}={value:1};',
    '  let before:number=box.value++;',
    '  return first+second+before;',
    '}',
    'export function count():number {',
    '  let n:number=0;',
    '  for(let i:number=0;i<3;i++){ n++; }',
    '  return n;',
    '}',
    'export function decrement():number {',
    '  let n:number=3;',
    '  let old:number=n--;',
    '  return old*10+n;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {compute as calculate,count,decrement} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {values} from "./data.js";',
    'import {calculate,count,decrement} from "./bridge.js";',
    'export const result:number=calculate(values);',
    'export const first:number=values[0];',
    'export const second:number=values[1];',
    'export const iterations:number=count();',
    'export const postDec:number=decrement();'
  ].join('\n'));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  function compile(source:string){
    const output=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(output.error,undefined,String(output.error));
    assert.equal(output.signal,null,'compiler timed out or crashed');
    return output;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['data','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const evaluated=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,first,second,iterations,postDec} from "./entry.js"; console.log(JSON.stringify({result,first,second,iterations,postDec}));'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(evaluated.error,undefined,String(evaluated.error));
  assert.equal(evaluated.status,0,evaluated.stdout+evaluated.stderr);
  assert.deepEqual(JSON.parse(evaluated.stdout),
    {result:42,first:21,second:22,iterations:3,postDec:32});
  const invalid=[
    ['constant','const n:number=1; let old:number=n++;','TS2588'],
    ['readonly','interface D {readonly value:number} let d:D={value:2}; let old:number=d.value++;','TS2540'],
    ['string-index','let s:string="ab"; let old:number=s[0]++;','TS2542'],
    ['non-numeric','let s:string="ab"; let old:number=s++;','TSCS1001'],
    ['invalid-target','let n:number=1; let old:number=(n+1)++;','TSCS1001']
  ] as const;
  for(const [name,source,diagnostic] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const result=compile(name+'.ts');
    assert.notEqual(result.status,0,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(diagnostic));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid program emitted '+name);
  }
  console.log('Postfix numeric expression: ES2015 four-module result=42 first=21 second=22 iterations=3 postDec=32; five negatives block emission.');
} finally {rmSync(folder,{recursive:true,force:true});}
