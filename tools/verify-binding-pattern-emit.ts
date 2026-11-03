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
    'model.ts':'export const box={value:21,nested:{tag:"ok"}};export const readings:number[]=[10,20,30,40];export const packet={"data-key":21,0:"ready",nested:{"score-total":42}};',
    'logic.ts':[
      'export function add(a:number,b:number):number{return a+b;}',
      'export function unpack({value}:{value:number}):number{return value*2;}',
      'export function pickArray([first,,third]:number[]):number{return first+third;}',
      'export function pickNested({inner:{score=42}}:{inner:{score?:number}}):number{return score;}',
      'export function modify({value}:{value:number}):number{value=value+1;return value;}',
      'export const arrowPick:(input:{value:number})=>number=({value}:{value:number}):number=>value*3;',
      'export const arrowArray:(input:number[])=>number=([first,,third]:number[]):number=>first+third;',
      'export const expressionPick:(input:{value:number})=>number=function({value}:{value:number}):number{return value+1;};',
      'export const arrowDefault:(input:{value?:number})=>number=({value=42}:{value?:number}):number=>value;',
      'export const methodPick:{read(input:{value:number}):number}={read({value}:{value:number}):number{return value;}};',
      'export function literalPick({"data-key":value}:{"data-key":number}):number{return value*2;}',
      'export function integerPick({0:value}:{0:number}):number{return value+1;}',
      'export function computedPick({["data-key"]:value}:{"data-key":number}):number{return value+1;}',
      'export function computedZero({[0]:value}:{0:number}):number{return value+2;}'
    ].join('\n'),
    'bridge.ts':'export {add,unpack,pickArray,pickNested,modify,arrowPick,arrowArray,expressionPick,arrowDefault,methodPick,literalPick,integerPick,computedPick,computedZero} from "./logic.js";',
    'entry.ts':[
      'import {box,readings,packet} from "./model.js";import {add,unpack,pickArray,pickNested,modify,arrowPick,arrowArray,expressionPick,arrowDefault,methodPick,literalPick,integerPick,computedPick,computedZero} from "./bridge.js";',
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
      'export const optionalUndefinedValue:number|undefined=optionalUndefined;',
      'export const parameterResult:number=unpack(box)+pickArray(readings)+pickNested({inner:{}})+modify(box);',
      'export const expressionBindings:number=arrowPick(box)+arrowArray(readings)+expressionPick(box)+arrowDefault({})+methodPick.read(box);',
      'const {"data-key":dataKey,0:caption,nested:{"score-total":scoreTotal}}=packet;',
      'const {"not-here":missingLiteral=7}={};',
      'const {0:missingNumeric=8}={};',
      'export const staticKeyResult:number=literalPick(packet)+integerPick({0:21})+scoreTotal+dataKey;',
      'export const staticKeyLabel:string=caption;',
      'export const staticKeyDefaults:number=missingLiteral+missingNumeric;',
      'const {["data-key"]:computed,[0]:computedLabel,nested:{["score-total"]:computedScore}}=packet;',
      'const {["missing"]:missingComputed=9}={};',
      'export const computedKeyResult:number=computedPick(packet)+computedZero({0:21})+computedScore+computed+missingComputed;',
      'export const computedKeyLabel:string=computedLabel;'
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
    'import {result,label,doubled,defaults,defaultCalls,undefinedOnly,optionalUndefinedValue,parameterResult,expressionBindings,staticKeyResult,staticKeyLabel,staticKeyDefaults,computedKeyResult,computedKeyLabel} from "./entry.js";'+
    'if(result!==83||label!=="ok"||doubled!==42||defaults!==135||defaultCalls!==3||undefinedOnly!==42||optionalUndefinedValue!==undefined||parameterResult!==146||expressionBindings!==188||staticKeyResult!==127||staticKeyLabel!=="ready"||staticKeyDefaults!==15||computedKeyResult!==117||computedKeyLabel!=="ready")throw Error(JSON.stringify({result,label,doubled,defaults,defaultCalls,parameterResult,expressionBindings,staticKeyResult,staticKeyLabel,staticKeyDefaults,computedKeyResult,computedKeyLabel}));'],
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
    ['wrong-annotation','let {value}:{value:number}={value:"bad"};','TS2322',2],
    ['parameter-missing','function f({missing}:{value:number}):number{return missing;}','TS2339',2],
    ['parameter-return','function f({value}:{value:number}):string{return value;}','TS2322',2],
    ['parameter-argument','function f({value}:{value:number}):number{return value;}const input={value:"no"};let n:number=f(input);','TS2345',2],
    ['parameter-duplicate','function f({x}:{x:number},x:number):number{return x;}','TS2300',2],
    ['parameter-later-default','function f({x}:{x:number},y:number=x):number{return y;}','TSCS1001',1],
    ['parameter-scope','function f({value}:{value:number}):number{return value;}value=21;','TS2304',2],
    ['parameter-body-shadow','function seed():number{return 1;}function f({x=seed()}:{x?:number}):number{function seed():number{return 2;}return x;}let n:number=f({});','TSCS1001',1],
    ['parameter-whole-self','let x:number=42;function f({x}:{x:number}={x:x}):number{return x;}let n:number=f();','TSCS1001',1],
    ['parameter-element-self','let x:number=42;function f({x=x}:{x?:number}):number{return x;}let n:number=f({});','TSCS1001',1],
    ['parameter-template-self','let x:string="outer";function f({x}:{x:string}={x:`\u0024{x}`}):string{return x;}let n:string=f();','TSCS1001',1],
    ['expression-missing','const f=({missing}:{value:number}):number=>missing;','TS2339',2],
    ['expression-return','const f=([value]:number[]):string=>value;','TS2322',2],
    ['expression-function','const f=function({value}:{value:number}):string{return value;};','TS2322',2],
    ['expression-duplicate','const f=({value}:{value:number},value:number):number=>value;','TS2300',2],
    ['literal-key-missing','const {"data-key":label}={other:21};','TS2339',2],
    ['literal-key-type','const {0:zero}={0:21};let wrong:string=zero;','TS2322',2],
    ['literal-key-noalias','const {"data-key"}={"data-key":21};','TSCS1001',1],
    ['literal-key-dynamic','const key="data-key";const {[key]:v}={"data-key":21};','TSCS1001',1],
    ['computed-key-missing','const {["missing"]:value}={"data-key":21};','TS2339',2],
    ['computed-key-type','const {[0]:value}={0:21};let wrong:string=value;','TS2322',2],
    ['computed-key-noalias','const {["data-key"]}={"data-key":21};','TSCS1001',1],
    ['computed-key-fraction','const {[0.5]:value}={0:21};','TSCS1001',1]
  ] as const;
  for(const [name,source,code,status] of negative) {
    writeFileSync(join(dir,'invalid-'+name+'.ts'),source);
    const bad=compile('invalid-'+name+'.ts');
    assert.equal(bad.status,status,bad.stdout+bad.stderr);
    assert.match(bad.stdout+bad.stderr,new RegExp(code));
    assert.equal(existsSync(join(dir,'invalid-'+name+'.js')),false,'invalid program emitted JS: '+name);
  }
  console.log('Finite object/array binding patterns: four linked ES2015 modules executed; thirty-three invalid programs emitted no JavaScript.');
} finally {rmSync(dir,{recursive:true,force:true});}
