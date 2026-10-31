import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four checked linked ES2015 modules executed by Node; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-condition-truthiness-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export interface Box { value:number; name:string }\nexport type Maybe = {value:number}|null;\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type {Box,Maybe} from "./model.js";',
    'export function member(box:Box):number{if(box.value){return box.value;}return 0;}',
    'export function called(fn:()=>number):number{if(fn()){return 1;}return 0;}',
    'export function named(box:Box):string{return box.name?"filled":"empty";}',
    'export function countWhile(n:number):number{let count:number=0;while(n){count=count+1;n=n-1;}return count;}',
    'export function countDo(n:number):number{let count:number=0;do{count=count+1;n=n-1;}while(n);return count;}',
    'export function countFor(n:number):number{let count:number=0;for(;n;){count=count+1;n=n-1;}return count;}',
    'export function optional(box:Maybe):number{if(box?.value){return 1;}return 0;}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export {member,called,named,countWhile,countDo,countFor,optional} from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import {member,called,named,countWhile,countDo,countFor,optional} from "./bridge.js";',
    'let calls:number=0;',
    'function next():number{calls=calls+1;return 21;}',
    'export const memberHit:number=member({value:42,name:"ready"});',
    'export const memberMiss:number=member({value:0,name:""});',
    'export const callHit:number=called(next);',
    'export const callCount:number=calls;',
    'export const labelHit:string=named({value:1,name:"ready"});',
    'export const labelMiss:string=named({value:1,name:""});',
    'export const whileCount:number=countWhile(3);',
    'export const doCount:number=countDo(1);',
    'export const forCount:number=countFor(4);',
    'export const nullMiss:number=optional(null);',
    'export const optionalHit:number=optional({value:21});'
  ].join('\n'));
  writeFileSync(join(folder,'tsconfig.json'),JSON.stringify({files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}}));
  const compile=(args:string[])=>{
    const r=spawnSync(process.env.DOTNET??'dotnet',[dll,...args],{cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
    assert.equal(r.error,undefined,String(r.error));
    assert.equal(r.signal,null,'compiler signal or timeout');
    return r;
  };
  const built=compile(['--emitJs','-p','tsconfig.json']);
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['model','logic','bridge','entry'])assert.ok(existsSync(join(folder,name+'.js')),'missing '+name);
  const run=spawnSync(process.execPath,['--input-type=module','--eval',[
    'import {memberHit,memberMiss,callHit,callCount,labelHit,labelMiss,whileCount,doCount,forCount,nullMiss,optionalHit} from "./entry.js";',
    'if(memberHit!==42||memberMiss!==0||callHit!==1||callCount!==1||labelHit!=="filled"||labelMiss!=="empty"||whileCount!==3||doCount!==1||forCount!==4||nullMiss!==0||optionalHit!==1)throw Error("runtime condition mismatch");'
  ].join('\n')],{cwd:folder,encoding:'utf8',timeout:15000,env});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  const invalid=[
    ['void-if','function bad():number{if(void 0){return 1;}return 0;}','TS2873'],
    ['void-ternary','function bad():number{return (void 0)?1:0;}','TS2873'],
    ['void-while','function bad(n:number):number{while(void n){n=n-1;}return n;}','TS2873'],
    ['void-do','function bad(n:number):number{do{n=n-1;}while(void n);return n;}','TS2873'],
    ['void-for','function bad(n:number):number{for(;void n;){n=n-1;}return n;}','TS2873'],
    ['missing','function bad(box:{value:number}):number{if(box.missing){return 1;}return 0;}','TS2339'],
    ['void-call','function noop():void{return;}function bad():number{if(noop()){return 1;}return 0;}','TS1345']
  ] as const;
  for(const [name,source,code] of invalid){
    writeFileSync(join(folder,name+'.ts'),source);
    const r=compile(['--emitJs','--noLib','--target','es2015',name+'.ts','minimal.d.ts']);
    assert.equal(r.status,2,r.stdout+r.stderr);
    assert.match(r.stdout+r.stderr,new RegExp(code));
    assert.equal(existsSync(join(folder,name+'.js')),false,'invalid JS output: '+name);
  }
  console.log('Runtime truthiness: four linked modules executed; seven invalid programs blocked.');
} finally {rmSync(folder,{recursive:true,force:true});}
