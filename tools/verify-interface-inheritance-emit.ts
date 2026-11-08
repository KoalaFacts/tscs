import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {runTscs} from './tscs-process.ts';
const dir=mkdtempSync(join(tmpdir(),'tscs-interface-heritage-'));
try {
  writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  for(const [name,source] of Object.entries({
    'model.ts':'export interface Base{readonly value:number}export interface Child extends Base{extra:number}export const seed:Child={value:20,extra:22};',
    'logic.ts':'import type {Child} from "./model.js";export function sum(data:Child):number{return data.value+data.extra;}',
    'bridge.ts':'export {seed} from "./model.js";export {sum} from "./logic.js";',
    'entry.ts':'import type {Child} from "./model.js";import {seed,sum} from "./bridge.js";const pair:Child=seed;export const result:number=sum(pair);'
  }))writeFileSync(join(dir,name),source);
  writeFileSync(join(dir,'project.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
      function compile(args:string[]){return runTscs(dir,args);}
  const compiled=compile(['--emitJs','-p',join(dir,'project.json')]);
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])assert.ok(existsSync(join(dir,name+'.js')));
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result} from "./entry.js";if(result!==42)throw Error("expected 42, got "+result);'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);

  // A2.3: shared members whose generic union arms or object fields appear
  // in a different order must survive checking, multi-module emit and Node
  // execution. These are real ES2015 modules, not just declaration tests.
  for(const [name,source] of Object.entries({
    'generic-bases.ts':
      'export interface Left<T>{value:T|undefined;meta:{x:T;y:number}}' +
      'export interface Right<U>{value:undefined|U;meta:{y:number;x:U}}',
    'generic-model.ts':
      'import type {Left,Right} from "./generic-bases.js";' +
      'export interface Model<V> extends Left<V>,Right<V>{total:number}' +
      'export const item:Model<number>={value:2,meta:{x:2,y:4},total:6};',
    'generic-logic.ts':
      'import type {Model} from "./generic-model.js";' +
      'export function sum(m:Model<number>):number{return m.meta.x+m.total;}',
    'generic-entry.ts':
      'import {item} from "./generic-model.js";import {sum} from "./generic-logic.js";' +
      'export const result:number=sum(item);'
  }))writeFileSync(join(dir,name),source);
  writeFileSync(join(dir,'generic-project.json'),JSON.stringify({
    files:['generic-entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const generic=compile(['--emitJs','-p',join(dir,'generic-project.json')]);
  assert.equal(generic.status,0,generic.stdout+generic.stderr);
  for(const name of ['generic-bases','generic-model','generic-logic','generic-entry'])
    assert.ok(existsSync(join(dir,name+'.js')),name+' missing emitted JS');
  const genericRuntime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result} from "./generic-entry.js";if(result!==8)throw Error("expected 8, got "+result);'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(genericRuntime.error,undefined,String(genericRuntime.error));
  assert.equal(genericRuntime.status,0,genericRuntime.stdout+genericRuntime.stderr);

  writeFileSync(join(dir,'generic-bad.ts'),
    'interface A<T>{item:T|number}interface B<U>{item:U|string}' +
    'interface N<V> extends A<V>,B<V>{}let answer:number=7;');
  const genericBad=compile(['--emitJs','--noLib','--target','es2015',
    'generic-bad.ts','minimal.d.ts']);
  assert.equal(genericBad.status,1,genericBad.stdout+genericBad.stderr);
  assert.match(genericBad.stdout+genericBad.stderr,/TSCS1001/);
  assert.equal(existsSync(join(dir,'generic-bad.js')),false);


  // Pinned Go accepts the reordered *concrete* tag union (reviewed in #249).
  // Exercise four actual ES2015 modules and the neighbouring bad case.
  for(const [suffix,baseTag,derivedTag] of [
    ['forward','string|1','1|string'],
    ['reverse','1|string','string|1']
  ]){
    const prefix='concrete-'+suffix;
    for(const [name,source] of Object.entries({
      [prefix+'-base.ts']:
        'export interface Base<T>{value:T;tag:'+baseTag+'}',
      [prefix+'-model.ts']:
        'import type {Base} from "./'+prefix+'-base.js";' +
        'export interface Box<T> extends Base<T>{tag:'+derivedTag+'}' +
        'export const item:Box<number>={value:8,tag:1};',
      [prefix+'-logic.ts']:
        'import type {Box} from "./'+prefix+'-model.js";' +
        'export function read(box:Box<number>):number{return box.value;}',
      [prefix+'-entry.ts']:
        'import {item} from "./'+prefix+'-model.js";' +
        'import {read} from "./'+prefix+'-logic.js";' +
        'export const result:number=read(item);'
    }))writeFileSync(join(dir,name),source);
    writeFileSync(join(dir,prefix+'-project.json'),JSON.stringify({
      files:[prefix+'-entry.ts','minimal.d.ts'],
      compilerOptions:{module:'es2015',target:'es2015',noLib:true}
    }));
    const checked=compile(['--emitJs','-p',join(dir,prefix+'-project.json')]);
    assert.equal(checked.status,0,prefix+': '+checked.stdout+checked.stderr);
    for(const name of ['base','model','logic','entry'])
      assert.ok(existsSync(join(dir,prefix+'-'+name+'.js')),prefix+' '+name+' output');
    const runtime=spawnSync(process.execPath,['--input-type=module','--eval',
      'import {result} from "./'+prefix+'-entry.js";if(result!==8)throw Error("result "+result);'],
      {cwd:dir,encoding:'utf8',timeout:15000});
    assert.equal(runtime.error,undefined,String(runtime.error));
    assert.equal(runtime.status,0,prefix+': '+runtime.stdout+runtime.stderr);
  }
  writeFileSync(join(dir,'concrete-conflict.ts'),
    'interface Base<T>{value:T;tag:string|1}' +
    'interface Box<T> extends Base<T>{tag:number|string}' +
    'let answer:number=8;');
  const concreteBad=compile(['--emitJs','--noLib','--target','es2015',
    'concrete-conflict.ts','minimal.d.ts']);
  assert.equal(concreteBad.status,1,concreteBad.stdout+concreteBad.stderr);
  assert.match(concreteBad.stdout+concreteBad.stderr,/TSCS1001/);
  assert.equal(existsSync(join(dir,'concrete-conflict.js')),false);

  // A2.3: separate checked .d.ts interface declarations merge before the
  // consumer is checked; declarations erase while real ES module JS runs.
  writeFileSync(join(dir,'ambient-part-one.d.ts'),'interface MergedReading {value:number;}');
  writeFileSync(join(dir,'ambient-part-two.d.ts'),'interface MergedReading {label:string;}');
  writeFileSync(join(dir,'merged-entry.ts'),
    'const reading:MergedReading={value:21,label:"a"};export const mergedResult:number=reading.value*2;');
  const merged=compile(['--emitJs','--noLib','--target','es2015','--module','es2015',
    'merged-entry.ts','ambient-part-one.d.ts','ambient-part-two.d.ts','minimal.d.ts']);
  assert.equal(merged.status,0,merged.stdout+merged.stderr);
  assert.ok(existsSync(join(dir,'merged-entry.js')));
  assert.equal(existsSync(join(dir,'ambient-part-one.d.js')),false);
  assert.equal(existsSync(join(dir,'ambient-part-two.d.js')),false);
  const mergedRuntime=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {mergedResult} from "./merged-entry.js"; if(mergedResult!==42)throw Error("merged result "+mergedResult);'],
    {cwd:dir,encoding:'utf8',timeout:15000});
  assert.equal(mergedRuntime.error,undefined,String(mergedRuntime.error));
  assert.equal(mergedRuntime.status,0,mergedRuntime.stdout+mergedRuntime.stderr);
  writeFileSync(join(dir,'ambient-conflict.d.ts'),'interface MergedReading {value:string;}');
  writeFileSync(join(dir,'bad-merged-entry.ts'),'let x:number=1;');
  const conflict=compile(['--emitJs','--noLib','--target','es2015',
    'bad-merged-entry.ts','ambient-part-one.d.ts','ambient-conflict.d.ts','minimal.d.ts']);
  assert.equal(conflict.status,2,conflict.stdout+conflict.stderr);
  assert.match(conflict.stdout+conflict.stderr,/TS2717/);
  assert.equal(existsSync(join(dir,'bad-merged-entry.js')),false);
  // Declaration equality is not the spelling order. Intersections retain
  // their source shape even when the runtime-checking object is flattened.
  const {runInNewContext}=await import('node:vm');
  const typeFile='identity-common.d.ts';
  writeFileSync(join(dir,typeFile),
    'type A={a:number};type B={b:string};type P=A&B;type Q={a:number;b:string};'+
    'declare const first:P;declare const second:P;');
  const identities:[string,string,string,boolean][]=[
    ['order-object','interface Identity{value:{a:number;b:string}}',
      'interface Identity{value:{b:string;a:number}}',true],
    ['order-union','interface Identity{value:number|string}',
      'interface Identity{value:string|number}',true],
    ['same-intersection','interface Identity{value:{a:number}&{b:string}}',
      'interface Identity{value:{a:number}&{b:string}}',true],
    ['intersection-object','interface Identity{value:{a:number}&{b:string}}',
      'interface Identity{value:{a:number;b:string}}',false],
    ['intersection-array','interface Identity{value:({a:number}&{b:string})[]}',
      'interface Identity{value:{a:number;b:string}[]}',false],
    ['intersection-function','interface Identity{value:(x:{a:number}&{b:string})=>number}',
      'interface Identity{value:(x:{a:number;b:string})=>number}',false],
    // Derived syntax denotes its resulting declaration type (pinned Go accepts
    // the first three in both orders and both entry paths).
    ['derived-typeof','interface Identity{value:typeof first}',
      'interface Identity{value:typeof second}',true],
    ['derived-keyof','interface Identity{value:keyof P}',
      'interface Identity{value:"a"|"b"}',true],
    ['derived-indexed','interface Identity{value:P["a"]}',
      'interface Identity{value:number}',true],
    ['derived-indexed-conflict','interface Identity{value:P["b"]}',
      'interface Identity{value:number}',false],
    ['derived-typeof-intersection-object','interface Identity{value:typeof first}',
      'interface Identity{value:{a:number;b:string}}',false]
  ];
  for(const [id,one,two,allowed] of identities)
    for(const reversed of [false,true]){
      const prefix=id+(reversed?'-rev':'');
      const firstFile=prefix+'-first.d.ts';
      const secondFile=prefix+'-second.d.ts';
      const entryFile=prefix+'-entry.ts';
      writeFileSync(join(dir,firstFile),reversed?two:one);
      writeFileSync(join(dir,secondFile),reversed?one:two);
      writeFileSync(join(dir,entryFile),'export const identityResult:number=7;');
      const checked=compile(['--emitJs','--noLib','--target','es2015','--module','es2015',
        entryFile,typeFile,firstFile,secondFile,'minimal.d.ts']);
      assert.equal(checked.status,allowed?0:2,prefix+': '+checked.stdout+checked.stderr);
      if(allowed){
        const ran=spawnSync(process.execPath,['--input-type=module','--eval',
          'import {identityResult} from "./'+prefix+'-entry.js";if(identityResult!==7)throw Error("identity");'],
          {cwd:dir,encoding:'utf8',timeout:15000});
        assert.equal(ran.error,undefined,String(ran.error));
        assert.equal(ran.status,0,ran.stdout+ran.stderr);
      }else{
        assert.match(checked.stdout+checked.stderr,/TS2717/,prefix);
        assert.equal(existsSync(join(dir,prefix+'-entry.js')),false,prefix);
      }
      assert.equal(existsSync(join(dir,prefix+'-first.d.js')),false,prefix);
      assert.equal(existsSync(join(dir,prefix+'-second.d.js')),false,prefix);
    }
  // The retained #237 26-case oracle counterexample contract: 11 paired
  // declaration inputs in both file orders and two callable-union neighbours
  // in both type orders. Pinned Go outcomes were captured before this fix.
  // Execute actual compiler JS for positives and assert no JS for conflicts.
  const varCases:{id:string;a:string;b:string;valid:boolean;read:string;value:unknown}[]=[
    {id:'var-object-reorder',a:'{a:number;b:string}',b:'{b:string;a:number}',
      valid:true,read:'item.a',value:{a:7,b:'ok'}},
    {id:'var-union-reorder',a:'number|string',b:'string|number',
      valid:true,read:'typeof item==="number"?item:0',value:7},
    {id:'var-alias-intersection-union',a:'(A&B)|Q',b:'Q|(A&B)',
      valid:true,read:'item.a',value:{a:7,b:'ok'}},
    {id:'var-inline-intersection-union',
      a:'({a:number}&{b:string})|{a:number;b:string}',
      b:'{a:number;b:string}|({a:number}&{b:string})',
      valid:true,read:'item.a',value:{a:7,b:'ok'}},
    {id:'var-nested-intersection-union',a:'{value:(A&B)|Q}',b:'{value:Q|(A&B)}',
      valid:true,read:'item.value.a',value:{value:{a:7,b:'ok'}}},
    {id:'var-any-conflict',a:'any',b:'number',valid:false,read:'7',value:7},
    {id:'var-primitive-conflict',a:'number',b:'string',valid:false,read:'7',value:7},
    {id:'var-intersection-object',a:'{a:number}&{b:string}',
      b:'{b:string;a:number}',valid:false,read:'7',value:{a:7,b:'ok'}},
    {id:'var-nested-intersection-object',a:'{value:{a:number}&{b:string}}',
      b:'{value:{b:string;a:number}}',valid:false,read:'7',value:{value:{a:7,b:'ok'}}},
    {id:'var-function-intersection',
      a:'(x:{a:number}&{b:string})=>number',
      b:'(x:{b:string;a:number})=>number',valid:false,read:'7',value:()=>7},
    {id:'var-array-intersection',a:'({a:number}&{b:string})[]',
      b:'{b:string;a:number}[]',valid:false,read:'7',value:[{a:7,b:'ok'}]},
    {id:'var-derived-typeof',a:'typeof first',b:'typeof second',
      valid:true,read:'item.a',value:{a:7,b:'ok'}},
    {id:'var-derived-keyof',a:'keyof P',b:'"a"|"b"',
      valid:true,read:'item==="a"?7:0',value:'a'},
    {id:'var-derived-indexed',a:'P["a"]',b:'number',valid:true,read:'item',value:7},
    {id:'var-derived-indexed-conflict',a:'P["b"]',b:'number',valid:false,read:'7',value:7},
    {id:'var-derived-typeof-intersection-object',a:'typeof first',b:'{a:number;b:string}',
      valid:false,read:'7',value:{a:7,b:'ok'}}
  ];
  for(const identity of varCases)
    for(const reverse of [false,true]){
      const id=identity.id+(reverse?'-reverse':'');
      const one=id+'-first.d.ts',two=id+'-second.d.ts',entry=id+'-entry.ts';
      writeFileSync(join(dir,one),'declare var item:'+(reverse?identity.b:identity.a)+';');
      writeFileSync(join(dir,two),'declare var item:'+(reverse?identity.a:identity.b)+';');
      writeFileSync(join(dir,entry),'let answer:number='+identity.read+';');
      const checked=compile(['--emitJs','--noLib','--target','es2015',
        entry,typeFile,one,two,'minimal.d.ts']);
      assert.equal(checked.status,identity.valid?0:2,id+': '+checked.stdout+checked.stderr);
      const output=join(dir,entry.replace(/\.ts$/,'.js'));
      if(identity.valid){
        assert.equal(existsSync(output),true,id+' missing emitted JS');
        assert.equal(runInNewContext(readFileSync(output,'utf8')+'\nanswer;',
          {item:identity.value}),7,id+' result');
      }else{
        assert.match(checked.stdout+checked.stderr,/TS2403/,id);
        assert.equal(existsSync(output),false,id+' must reject emit');
      }
      assert.equal(existsSync(join(dir,one.replace(/\.d\.ts$/,'.d.js'))),false,id);
      assert.equal(existsSync(join(dir,two.replace(/\.d\.ts$/,'.d.js'))),false,id);
    }
  // typeof of inferred literal values and of same-signature function
  // declarations denotes their declared type (pinned Go accepts all three
  // in both orders); a reviewer-reported regression on 2d3bb1d.
  const valueFile='identity-values.ts';
  writeFileSync(join(dir,valueFile),'let n1=7;let n2=8;const o1={a:7,b:"ok"};const o2={a:8,b:"yes"};'+
    'function f1(v:P):number{return v.a}function f2(v:P):number{return v.a}');
  const queryCases:{id:string;a:string;b:string;read:string;value:unknown}[]=[
    {id:'query-inferred-number',a:'n1',b:'n2',read:'item',value:7},
    {id:'query-inferred-object',a:'o1',b:'o2',read:'item.a',value:{a:7,b:'ok'}},
    {id:'query-function-signature',a:'f1',b:'f2',read:'item({a:7,b:"ok"})',
      value:(v:{a:number})=>v.a}
  ];
  for(const identity of queryCases)
    for(const reverse of [false,true]){
      const id=identity.id+(reverse?'-reverse':'');
      const one=id+'-first.d.ts',two=id+'-second.d.ts',entry=id+'-entry.ts';
      writeFileSync(join(dir,one),'declare var item:typeof '+(reverse?identity.b:identity.a)+';');
      writeFileSync(join(dir,two),'declare var item:typeof '+(reverse?identity.a:identity.b)+';');
      writeFileSync(join(dir,entry),'let answer:number='+identity.read+';');
      rmSync(join(dir,'identity-values.js'),{force:true});
      const checked=compile(['--emitJs','--noLib','--target','es2015',
        entry,valueFile,typeFile,one,two,'minimal.d.ts']);
      assert.equal(checked.status,0,id+': '+checked.stdout+checked.stderr);
      const output=join(dir,entry.replace(/\.ts$/,'.js'));
      assert.equal(runInNewContext(readFileSync(output,'utf8')+'\nanswer;',
        {item:identity.value}),7,id+' result');
    }
  for(const member of [false,true])
    for(const reversed of [false,true]){
      const id='ordinary-union-call-'+(member?'member-':'direct-')+(reversed?'reverse':'forward');
      const entry=id+'.ts',ambient=id+'.d.ts';
      const sigs=reversed?'((v:Q)=>number)|((v:P)=>number)':
        '((v:P)=>number)|((v:Q)=>number)';
      const declaration=member?'declare const obj:{run:'+sigs+'};':
        'declare const call:'+sigs+';';
      writeFileSync(join(dir,ambient),declaration);
      writeFileSync(join(dir,entry),
        'let answer:number='+(member?'obj.run':'call')+'({a:7,b:"ok"});');
      const compiled=compile(['--emitJs','--noLib','--target','es2015',
        entry,typeFile,ambient,'minimal.d.ts']);
      assert.equal(compiled.status,0,id+': '+compiled.stdout+compiled.stderr);
      const output=join(dir,entry.replace(/\.ts$/,'.js'));
      assert.equal(runInNewContext(readFileSync(output,'utf8')+'\nanswer;',
        member?{obj:{run:(value:{a:number})=>value.a}}:
          {call:(value:{a:number})=>value.a}),7,id);
      assert.equal(existsSync(join(dir,ambient.replace(/\.d\.ts$/,'.d.js'))),false,id);
    }
  // Shared optional member identity includes implicit undefined at nested positions.
  // Recheck actual emit/runtime in both base orders; conflicts must not emit.
  const optionalShared:[string,string,boolean][]=[
    ["direct","interface A{x:{v?:number}}interface B{x:{v?:number|undefined}}",true],
    ["readonly","interface A{x:{readonly v?:number}}interface B{x:{readonly v?:number|undefined}}",true],
    ["alias","type L={v?:number};type R={v?:number|undefined};interface A{x:L}interface B{x:R}",true],
    ["deep","interface A{x:{inner:{v?:number}}}interface B{x:{inner:{v?:number|undefined}}}",true],
    ["array","interface A{x:{v?:number}[]}interface B{x:{v?:number|undefined}[]}",true],
    ["callback_return","interface A{x:()=>{v?:number}}interface B{x:()=>{v?:number|undefined}}",true],
    ["callback_parameter","interface A{x:(item:{v?:number})=>number}interface B{x:(item:{v?:number|undefined})=>number}",true],
    ["union_order","interface A{x:{v?:number}|string}interface B{x:string|{v?:undefined|number}}",true],
    ["nested_two_optional","interface A{x:{inner?:{v?:number}}}interface B{x:{inner?:{v?:number|undefined}|undefined}}",true],
    ["bad_type","interface A{x:{v?:number}}interface B{x:{v?:string}}",false],
    ["required_undefined","interface A{x:{v:number}}interface B{x:{v:number|undefined}}",false],
    ["optional_any","interface A{x:{v?:any}}interface B{x:{v?:number}}",false],
    ["readonly_mismatch","interface A{x:{readonly v?:number}}interface B{x:{v?:number|undefined}}",false],
  ];
  for(const [name,prelude,allowed] of optionalShared)
    for(const order of ['A,B','B,A']){
      const path=join(dir,'optional-shared-'+name+'-'+order.replace(',','-')+'.ts');
      writeFileSync(path,prelude+'interface N extends '+order+'{}let answer:number=7;');
      const result=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
      assert.equal(result.status,allowed?0:1,name+' '+order+': '+result.stdout+result.stderr);
      const output=path.replace(/\.ts$/,'.js');
      if(allowed){
        assert.equal(existsSync(output),true,name+' missing emitted JS');
        assert.equal(runInNewContext(readFileSync(output,'utf8')+'\nanswer;'),7,name);
      }else{
        assert.match(result.stdout+result.stderr,/TSCS1001/,name);
        assert.equal(existsSync(output),false,name+' must reject emit');
      }
    }
  console.log('shared optional identity: 18 valid emits execute to 7; 8 conflicting emits rejected.');
  const invalid:[string,string,string,number][]=[
    ['wrong-value','interface A{value:number}interface B extends A{}let b:B={value:"bad"};','TS2322',2],
    ['readonly-parent','interface A{readonly value:number}interface B extends A{}let b:B={value:1};b.value=2;','TS2540',2],
    ['wrong-key','interface A{value:number}interface B extends A{}let k:keyof B="missing";','TS2322',2],
    ['override','interface A{value:number}interface B extends A{value:string}','TS2430',2],
    ['missing-base','interface B extends Missing{value:number}','TS2304',2],
    ['primitive-base','type Num=number;interface B extends Num{value:number}','TSCS1001',1],
    ['cycle','interface A extends B{}interface B extends A{}','TS2456',2]
  ];
  for(const [name,source,code,exit] of invalid){
    const path=join(dir,'bad-'+name+'.ts');writeFileSync(path,source);
    const result=compile(['--emitJs','--noLib','--target','es2015',path,join(dir,'minimal.d.ts')]);
    assert.equal(result.status,exit,result.stdout+result.stderr);
    assert.match(result.stdout+result.stderr,new RegExp(code));
    assert.equal(existsSync(path.replace(/\.ts$/,'.js')),false,'invalid source emitted JavaScript');
  }
  console.log('finite interface heritage: four ES2015 modules emitted, result=42; seven invalid programs rejected.');
} finally {rmSync(dir,{recursive:true,force:true});}
