import {test} from 'node:test';
import assert from 'node:assert/strict';
import {reconcileResults,buildFixtureHarness,validateReferenceBindings} from './fixture-corpus.ts';
import {mkdtempSync,writeFileSync,readFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {execFileSync} from 'node:child_process';

const normal={units:[],links:[],directory:'',options:{},error:null,panic:null,calls:1};

// Catch missing/duplicate results being counted as matches, and nil normalization.
test('raw comparison preserves nil and metadata differences without awarding compiler credit',()=>{
 const sources=[{id:'a',rawSha256:'source-a',upstreamSelection:{status:'UPSTREAM_SKIPPED'}},{id:'b',rawSha256:'source-b'}];
 const result=reconcileResults(sources,[{id:'a',expected:{...normal,units:null}},{id:'b',expected:{...normal,options:{x:'YQ=='}}}],
  [{id:'a',expected:{...normal,units:[]}},{id:'b',expected:{...normal,options:{x:'Yg=='}}}]);
 assert.deepEqual(result.map(x=>x.loading),['MISMATCH','MISMATCH']);
 assert.equal(result[0].upstreamSelection.status,'UPSTREAM_SKIPPED');
 for(const r of result)assert.deepEqual(r.progress,{compiler:'NOT_RUN',reference:'NOT_RUN',acceptance:'NOT_RUN'});
});
test('raw comparison rejects omissions, duplicate identities and extra results',()=>{
 const source=[{id:'a'}],one=[{id:'a',expected:{...normal,units:[]}}];
 for(const rows of [[],[...one,...one],[...one,{id:'extra',expected:{}}]])
  assert.throws(()=>reconcileResults(source,rows,one),/identit|result/i);
 assert.throws(()=>reconcileResults([...source,...source],one,one),/identit/i);
});
test('raw comparison ignores object property order but retains panic status',()=>{
 const out=reconcileResults([{id:'a'},{id:'b'}],[{id:'a',expected:{...normal,units:[],calls:1}},{id:'b',expected:{panic:'bad',calls:0}}],
 [{id:'a',expected:{...normal,calls:1,units:[]}},{id:'b',expected:{calls:0,panic:'bad'}}]);
 assert.equal(out[0].loading,'MATCH');assert.equal(out[0].actualSha256,out[0].referenceSha256);
 assert.equal(out[1].loading,'REFERENCE_PANIC');
});

 test('malformed result envelopes and byte fields cannot be accepted as matches',()=>{
  for(const expected of [{},[],{...normal,units:{}},{...normal,directory:'not base64'},
   {...normal,links:[{alias:''}]},{...normal,units:[{name:'',content:'',options:[]}]},{...normal,calls:-1}])
   assert.throws(()=>reconcileResults([{id:'a'}],[{id:'a',expected}],[{id:'a',expected}]),/payload/i);
 });
 test('empty panic and error strings retain reference failure classification',()=>{
  for(const [expected,loading] of [[{panic:'',calls:0},'REFERENCE_PANIC'],[{...normal,error:''},'REFERENCE_ERROR']])
   assert.equal(reconcileResults([{id:'a'}],[{id:'a',expected}],[{id:'a',expected}])[0].loading,loading);
 });
test('harness build executes edited sources and fails closed on a compiler error',()=>{
 const temp=mkdtempSync(join(tmpdir(),'tscs-corpus-build-')),dotnet=process.env.DOTNET??'dotnet';
 try {
  const project=join(temp,'Probe.csproj'),code=join(temp,'Program.cs'),dll=join(temp,'bin/Release/net10.0/Probe.dll');
  writeFileSync(project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>');
  writeFileSync(code,'System.Console.Write("before");');
  execFileSync(dotnet,['build',project,'-c','Release','-m:1','-p:UseSharedCompilation=false'],{stdio:'pipe'});
  writeFileSync(code,'System.Console.Write("after");');
  const log=join(temp,'build.log');buildFixtureHarness(dotnet,project,log);
  assert.equal(execFileSync(dotnet,[dll],{encoding:'utf8'}),'after');
  assert.match(readFileSync(log,'utf8'),/Build succeeded/);
  writeFileSync(code,'invalid C#');
  assert.throws(()=>buildFixtureHarness(dotnet,project,join(temp,'failed.log')),/build failed/i);
 } finally {rmSync(temp,{recursive:true,force:true});}
});
test('file-map profile compares rooted contents and retains raw alias collisions',()=>{
 const expected={directory:'Ly5zcmM=',files:[{path:'L2E=',content:'eDs='}],links:[{rawAlias:'eA==',rawTarget:'YQ==',alias:'L3g=',target:'L2E='},{rawAlias:'Li94',rawTarget:'Yg==',alias:'L3g=',target:'L2I='}],unitCount:2,panic:null};
 const rows=[{id:'a',expected}];assert.equal(reconcileResults([{id:'a'}],rows,rows,'file-map')[0].loading,'MATCH');
 const changed=[{id:'a',expected:{...expected,files:[{path:'L2E=',content:'eTs='}]}}];
 assert.equal(reconcileResults([{id:'a'}],changed,rows,'file-map')[0].loading,'MISMATCH');
 for(const invalid of [{}, {...expected,files:null},{...expected,links:[{alias:'L3g='}]}])assert.throws(()=>reconcileResults([{id:'a'}],[{id:'a',expected:invalid}],[{id:'a',expected:invalid}],'file-map'),/payload/i);
});

test('reference echoes must bind every result to the actual input bytes and file name',()=>{
 const requests=[{id:'a',input:'eDs=',fileName:'YS50cw=='}];
 validateReferenceBindings(requests,requests);
 for(const bad of [[],[...requests,...requests],[{...requests[0],input:'eTs='}],[{...requests[0],fileName:'Yi50cw=='}]])
  assert.throws(()=>validateReferenceBindings(requests,bad),/binding/i);
});
