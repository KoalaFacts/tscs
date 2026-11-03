import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

const folder=mkdtempSync(join(tmpdir(),'tscs-object-spreads-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),[
    'export type Data={value:number;label:string};',
    'export const source:Data={value:20,label:"source"};',
    'export const other={tail:1};',
    'export const empty={};',
    'export type Patch={value?:string};',
    'export const omitted:Patch={};',
    'export const present:Patch={value:"overlay"};',
    'export type ReadonlyPatch={item?:{readonly n:number}};',
    'export const absentReadonly:ReadonlyPatch={};',
    'export const presentReadonly:ReadonlyPatch={item:{n:42}};',
    'export type NumericPatch={0?:string};',
    'export const omittedNumeric:NumericPatch={};',
    'export const presentNumeric:NumericPatch={0:"overlay"};',
    'export const computedNumeric={[0]:21};',
    'export type SpreadVariant={value:number;tag:boolean}|{value:string;tag:boolean};',
    'export const numberChoice:SpreadVariant={value:21,tag:true};',
    'export const textChoice:SpreadVariant={value:"choice",tag:true};',
    'export type MaybeNumber={value:number}|null|undefined;',
    'export const nullValue:MaybeNumber=null;',
    'export const someValue:MaybeNumber={value:33};',
    'export type MaybeText={value?:string}|null;',
    'export const nullText:MaybeText=null;',
    'export const someText:MaybeText={value:"nullable"};',
    'export type OptionalChoice={value:number;tag:boolean}|{value?:string;tag:boolean};',
    'export const choiceNumber:OptionalChoice={value:31,tag:true};',
    'export const choiceMissing:OptionalChoice={tag:true};',
    'export const choiceString:OptionalChoice={value:"variant",tag:true};'
  ].join('\n'));
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Data,Patch,ReadonlyPatch,NumericPatch,SpreadVariant,MaybeNumber,MaybeText,OptionalChoice} from "./model.js";',
    'import {computedNumeric} from "./model.js";',
    'export function build(data:Data):Data{return {...data,value:data.value+22};}',
    'export function read(data:Data):number{return data.value;}',
    'export function mergeOptional(patch:Patch):{value:number|string}{return {value:21,...patch};}',
    'export function preferRequired(patch:Patch):number{return {...patch,value:42}.value;}',
    'export function readOptional(patch:Patch):number|string{return mergeOptional(patch).value;}',
    'export function mergeConst(patch:Patch):{value:21|string}{const v={value:21,...patch} as const;return v;}',
    'export function writeConst(patch:Patch):{value:21|string}{const v={value:21,...patch} as const;v.value="updated";return v;}',
    'export function nestedReadonly(patch:ReadonlyPatch):number{const v={item:{n:21},...patch};return v.item.n;}',
    'export function numericOptional(patch:NumericPatch):number|string{const v={0:21,...patch};let k:keyof typeof v="0";return v[0];}',
    'export function computedOptional(patch:NumericPatch):number|string{const v={...computedNumeric,...patch};let k:keyof typeof v=0;return v[0];}',
    'export function unionValue(value:SpreadVariant):number|string{const v={...value};return v.value;}',
    'export function unionConstValue(value:SpreadVariant):number|string{const v={...value,tail:true} as const;return v.value;}',
    'export function nullableValue(value:MaybeNumber):number|undefined{return {...value}.value;}',
    'export function nullablePrior(value:MaybeNumber):number{return {value:21,...value}.value;}',
    'export function nullableLast(value:MaybeNumber):number{return {...value,value:42}.value;}',
    'export function nullableConst(value:MaybeNumber):number|undefined{const snapshot={...value} as const;return snapshot.value;}',
    'export function nullableText(value:MaybeText):string|undefined{return {...value}.value;}',
    'export function choiceValue(x:OptionalChoice):number|string|undefined{return {...x}.value;}',
    'export function choiceFallback(x:OptionalChoice):number|string{return {value:17,...x}.value;}',
    'export function choiceLast(x:OptionalChoice):number{return {...x,value:42}.value;}',
    'export function choiceConst(x:OptionalChoice):number|string|undefined{const y={...x} as const;return y.value;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {build,read,mergeOptional,preferRequired,readOptional,mergeConst,writeConst,nestedReadonly,numericOptional,computedOptional,unionValue,unionConstValue,nullableValue,nullablePrior,nullableLast,nullableConst,nullableText,choiceValue,choiceFallback,choiceLast,choiceConst} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {source,other,empty,omitted,present,absentReadonly,presentReadonly,omittedNumeric,presentNumeric,numberChoice,textChoice,nullValue,someValue,nullText,someText,choiceNumber,choiceMissing,choiceString} from "./model.js";',
    'import {build,read,preferRequired,readOptional,mergeConst,writeConst,nestedReadonly,numericOptional,computedOptional,unionValue,unionConstValue,nullableValue,nullablePrior,nullableLast,nullableConst,nullableText,choiceValue,choiceFallback,choiceLast,choiceConst} from "./bridge.js";',
    'export const result:number=read(build(source));',
    'export const overridden:number=({...source,value:42}).value;',
    'export const ordered:number=({...source,...other,value:42}).value;',
    'export const nested:number=({...{data:source},data:{...source,value:42}}).data.value;',
    'export const copied:number=({...empty,...source,value:42}).value;',
    'export const unchanged:number=source.value;',
    'export const absent:number|string=readOptional(omitted);',
    'export const overlaid:number|string=readOptional(present);',
    'export const required:number=preferRequired(present);',
    'export const constAbsent:21|string=mergeConst(omitted).value;',
    'export const constPresent:21|string=mergeConst(present).value;',
    'export const constUpdated:21|string=writeConst(present).value;',
    'export const nestedMissing:number=nestedReadonly(absentReadonly);',
    'export const nestedPresent:number=nestedReadonly(presentReadonly);',
    'export const numericAbsent:number|string=numericOptional(omittedNumeric);',
    'export const numericPresent:number|string=numericOptional(presentNumeric);',
    'export const computedAbsent:number|string=computedOptional(omittedNumeric);',
    'export const computedPresent:number|string=computedOptional(presentNumeric);',
    'export const unionNumber:number|string=unionValue(numberChoice);',
    'export const unionText:number|string=unionValue(textChoice);',
    'export const unionConstNumber:number|string=unionConstValue(numberChoice);',
    'export const unionConstText:number|string=unionConstValue(textChoice);',
    'export const nullAbsent:number|undefined=nullableValue(nullValue);',
    'export const nullPresent:number|undefined=nullableValue(someValue);',
    'export const nullPrior:number=nullablePrior(nullValue);',
    'export const nullOverlay:number=nullablePrior(someValue);',
    'export const nullLast:number=nullableLast(someValue);',
    'export const nullConst:number|undefined=nullableConst(someValue);',
    'export const nullableStringAbsent:string|undefined=nullableText(nullText);',
    'export const nullableStringPresent:string|undefined=nullableText(someText);',
    'export const choiceRequired:number|string|undefined=choiceValue(choiceNumber);',
    'export const choiceAbsent:number|string|undefined=choiceValue(choiceMissing);',
    'export const choicePresent:number|string|undefined=choiceValue(choiceString);',
    'export const choiceBefore:number|string=choiceFallback(choiceMissing);',
    'export const choiceBeforePresent:number|string=choiceFallback(choiceString);',
    'export const choiceAfter:number=choiceLast(choiceString);',
    'export const choiceReadOnly:number|string|undefined=choiceConst(choiceNumber);'
  ].join('\n'));
  function compile(source:string){
    const r=spawnSync(process.env.DOTNET??'dotnet',
      [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
      {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler crashed or timed out');
    return r;
  }
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry']){
    const emitted=join(folder,name+'.js');
    assert.ok(existsSync(emitted),'missing '+name+'.js');
    // ES2015 target must not leak ES2018 object-spread syntax.
    assert.ok(!readFileSync(emitted,'utf8').includes('...'),'ES2018 spread leaked into emitted ES2015');
  }
  const runtime=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {result,overridden,ordered,nested,copied,unchanged,absent,overlaid,required,constAbsent,constPresent,constUpdated,nestedMissing,nestedPresent,numericAbsent,numericPresent,computedAbsent,computedPresent,unionNumber,unionText,unionConstNumber,unionConstText,nullAbsent,nullPresent,nullPrior,nullOverlay,nullLast,nullConst,nullableStringAbsent,nullableStringPresent,choiceRequired,choiceAbsent,choicePresent,choiceBefore,choiceBeforePresent,choiceAfter,choiceReadOnly} from "./entry.js";',
    'if(result!==42||overridden!==42||ordered!==42||nested!==42||copied!==42||unchanged!==20||absent!==21||overlaid!=="overlay"||required!==42||constAbsent!==21||constPresent!=="overlay"||constUpdated!=="updated"||nestedMissing!==21||nestedPresent!==42||numericAbsent!==21||numericPresent!=="overlay"||computedAbsent!==21||computedPresent!=="overlay"||unionNumber!==21||unionText!=="choice"||unionConstNumber!==21||unionConstText!=="choice"||nullAbsent!==undefined||nullPresent!==33||nullPrior!==21||nullOverlay!==33||nullLast!==42||nullConst!==33||nullableStringAbsent!==undefined||nullableStringPresent!=="nullable"||choiceRequired!==31||choiceAbsent!==undefined||choicePresent!=="variant"||choiceBefore!==17||choiceBeforePresent!=="variant"||choiceAfter!==42||choiceReadOnly!==31)',
    '  throw Error("object spread runtime mismatch");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(runtime.error,undefined,String(runtime.error));
  assert.equal(runtime.status,0,runtime.stdout+runtime.stderr);
  const invalid=[
    ['number','let x={...42};','TSCS1001'],
    ['boolean','let x={...true};','TSCS1001'],
    ['string','let x={..."bad"};','TSCS1001'],
    ['array','let x={...[1,2]};','TSCS1001'],
    ['unknown','let x={...missing};','TS2304'],
    ['wrong-spread','let x:{value:number}={...{value:"bad"}};','TS2322'],
    ['wrong-override','let x:{value:number}={...{value:20},value:"bad"};','TS2322'],
    ['extra-explicit','let x:{value:number}={...{value:21},extra:true};','TS2353'],
    ['optional-bad-number','let p:{value?:string}={};let x={value:21,...p};let n:number=x.value;','TS2322'],
    ['optional-bad-boolean','let p:{value?:string}={};let x={value:21,...p};x.value=true;','TS2322'],
    ['optional-const-bad-write','let p:{value?:string}={};const x={value:21,...p} as const;x.value=22;','TS2322'],
    ['optional-const-late-readonly','let p:{value?:string}={};const x={value:21,...p,tag:true} as const;x.value=21;','TS2540'],
    ['nested-readonly-unsafe-write','let p:{item?:{readonly n:number}}={};let x={item:{n:21},...p};x.item.n=22;','TS2540'],
    ['nested-union-readonly-unsafe-write','let p:{item?:{child:{n:number}|{readonly n:number}}}={};let x={item:{child:{n:21}},...p};x.item.child.n=22;','TS2540'],
    ['numeric-optional-keyof','let p:{0?:string}={};let v={0:21,...p};let k:keyof typeof v=0;','TS2322'],
    ['numeric-optional-value','let p:{"0"?:string}={};let v={0:21,...p};let n:number=v[0];','TS2322'],
    ['computed-numeric-keyof','let p:{0?:string}={};let v={[0]:21,...p};let k:keyof typeof v="0";','TS2322'],
    ['union-spread-wrong-value','function f(x:{value:number;tag:boolean}|{value:string;tag:boolean}):void{let y={...x};let bad:boolean=y.value;}','TS2322'],
    ['union-spread-correlated-unsupported','function f(x:{value:number;tag:boolean}|{value:string;tag:string}):void{let y={...x};}','TSCS1001'],
    ['nullable-spread-invalid-direct-read','function f(x:{value:number}|null):void{let y={...x};let bad:number=y.value;}','TS2322'],
    ['nullable-spread-const-write','function f(x:{value:number}|null):void{const y={...x} as const;y.value=42;}','TS2540'],
    ['nullable-spread-mixed-nonobject','function f(x:{value:number}|string):void{let y={...x};}','TSCS1001'],
    ['choice-unsafe-read','function f(x:{value:number;tag:boolean}|{value?:string;tag:boolean}):void{let y={...x};let n:number=y.value;}','TS2322'],
    ['choice-unsafe-write','function f(x:{value:number;tag:boolean}|{value?:string;tag:boolean}):void{let y={...x};y.value=true;}','TS2322'],
    ['choice-const-write','function f(x:{value:number;tag:boolean}|{value?:string;tag:boolean}):void{const y={...x} as const;y.value=42;}','TS2540'],
    ['choice-correlated-unsupported','function f(x:{value:number;tag?:string}|{value?:number;tag:string}):void{let y={...x};}','TSCS1001']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const checked=compile(name+'.ts');
    assert.notEqual(checked.status,0,checked.stdout+checked.stderr);
    assert.match(checked.stdout+checked.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid spread emitted');
  }
  console.log('Object spreads: four-module runtime, conditional const override values/writes, and twenty-six invalid no-emit programs.');
} finally {rmSync(folder,{recursive:true,force:true});}
