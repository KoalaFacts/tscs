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
      'interface Identity{value:(x:{a:number;b:string})=>number}',false]
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
        entryFile,firstFile,secondFile,'minimal.d.ts']);
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
  const {runInNewContext}=await import('node:vm');
  const typeFile='identity-common.d.ts';
  writeFileSync(join(dir,typeFile),
    'type A={a:number};type B={b:string};type P=A&B;type Q={a:number;b:string};');
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
      b:'{b:string;a:number}[]',valid:false,read:'7',value:[{a:7,b:'ok'}]}
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
