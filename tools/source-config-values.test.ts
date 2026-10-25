import test from 'node:test';import assert from 'node:assert/strict';import {readFileSync,mkdtempSync,writeFileSync,rmSync,mkdirSync,unlinkSync} from 'node:fs';import {tmpdir} from 'node:os';import {join} from 'node:path';
import {reconcileSourceConfigValues,verifySourceConfigValueArtifacts} from './source-config-values.ts';import {sha256} from './ts7-scope.ts';
const vectors=JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/source-config-values.json','utf8')).vectors;
function actual(v){return {id:v.id,expected:{preparation:{selection:v.selection,context:v.context,unsupported:null,ast:{unsupported:null,panic:null,diagnostics:v.reference.parserDiagnostics}},value:v.reference.value,conversionDiagnostics:v.reference.conversionDiagnostics,unsupported:null,outcome:v.reference.parserDiagnostics.length?'VALUE_DIAGNOSTICS':'VALUE'}};}
test('a real config syntax error remains MATCH_DIAGNOSTICS and never clean MATCH',()=>{const v=vectors.find(v=>v.reference.parserDiagnostics.length),a=actual(v);assert.equal(reconcileSourceConfigValues([v],[a])[0].status,'MATCH_DIAGNOSTICS');a.expected.preparation.ast.diagnostics=[];assert.equal(reconcileSourceConfigValues([v],[a])[0].status,'MISMATCH');});
test('selected content and rooted context mismatches cannot earn value matches',()=>{const v=vectors[0];for(const key of ['selection','context']){const a=structuredClone(actual(v));a.expected.preparation[key]={};assert.equal(reconcileSourceConfigValues([v],[a])[0].status,'MISMATCH');}});
test('changed converted values or extra conversion errors remain mismatches',()=>{const v=vectors[0];const a=structuredClone(actual(v));a.expected.value={...a.expected.value,kind:'Null'};assert.equal(reconcileSourceConfigValues([v],[a])[0].status,'MISMATCH');const b=structuredClone(actual(v));b.expected.conversionDiagnostics=[{code:'TS9999'}];assert.equal(reconcileSourceConfigValues([v],[b])[0].status,'MISMATCH');});
test('absent, unsupported and panic outcomes cannot earn matches',()=>{const v=vectors[0];for(const [outcome,status] of [['NOT_APPLICABLE','MISMATCH'],['UNSUPPORTED','UNSUPPORTED'],['PREPARATION_PANIC','CRASH']]){const a=actual(v);a.expected.outcome=outcome;const r=reconcileSourceConfigValues([v],[a])[0];assert.equal(r.status,status);assert.equal(r.equal,false);}});
test('missing, repeated or reordered actual identities reject the profile',()=>{const rows=vectors.slice(0,2);for(const a of [[],[actual(rows[0]),actual(rows[0])],[actual(rows[1]),actual(rows[0])]])assert.throws(()=>reconcileSourceConfigValues(rows,a));});
function evidence(action){
 const dir=mkdtempSync(join(tmpdir(),'tscs-config-values-'));
 try{
  const vs=structuredClone(vectors.slice(0,2)),counts={sources:2,selectedConfigs:2,noConfigSources:0,actualValueConfigs:2,parserErrorConfigs:vs.filter(v=>v.reference.parserDiagnostics.length).length,parserDiagnostics:vs.reduce((n,v)=>n+v.reference.parserDiagnostics.length,0),conversionDiagnostics:vs.reduce((n,v)=>n+v.reference.conversionDiagnostics.length,0),selectedUpstreamSkipped:vs.filter(v=>v.source.upstreamSelection.status==='UPSTREAM_SKIPPED').length,upstreamSkippedSources:0,nonRawUtf8Sources:0};
  const payload={
   'golden.json':JSON.stringify({upstreamCommit:'8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',profile:'selected-config-values/default-options/pre-vfs/no-notifier',counts,vectors:vs}),
   'generate.log':'', 'build.log':'', 'actual-000.jsonl':vs.map(v=>JSON.stringify(actual(v))).join('\n')+'\n','actual-000.jsonl.log':'',
   'actual-calls.json':JSON.stringify([{artifact:'actual-000.jsonl',offset:0,requests:2,status:0,stdoutSha256:sha256(vs.map(v=>JSON.stringify(actual(v))).join('\n')+'\n')}]),
   'results.jsonl':reconcileSourceConfigValues(vs,vs.map(actual)).map(r=>JSON.stringify(r)).join('\n')+'\n',
   'oracle/requests.json':JSON.stringify(vs.map(v=>v.request)),
   'oracle/selection.json':JSON.stringify(vs.map(v=>({id:v.id,input:v.request.input,fileName:v.request.fileName,expected:v.selection,context:v.context}))),
   'oracle/selection.log':'','oracle/parse-requests.json':JSON.stringify(vs.map(v=>({id:v.id,sourceBytes:v.reference.sourceBytes,fileName:v.reference.fileName}))),
   'oracle/reference.json':JSON.stringify(vs.map(v=>v.reference)),'oracle/reference.log':''
  };
  const manifest=names=>names.map(path=>({path,bytes:Buffer.byteLength(payload[path]),sha256:sha256(payload[path])}));
  payload['oracle/summary.json']=JSON.stringify({counts,artifacts:manifest(Object.keys(payload).filter(p=>p.startsWith('oracle/'))).map(a=>({...a,path:a.path.slice(7)}))});
  for(const status of ['MATCH','MATCH_DIAGNOSTICS','MISMATCH','UNSUPPORTED','CRASH'])counts[status]=reconcileSourceConfigValues(vs,vs.map(actual)).filter(r=>r.status===status).length;
  const report={counts,upstreamCommit:'8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',profile:'selected-config-values/default-options/pre-vfs/no-notifier',artifacts:manifest(Object.keys(payload))};
  mkdirSync(join(dir,'oracle'));for(const [path,body] of Object.entries(payload))writeFileSync(join(dir,path),body);
  const save=()=>writeFileSync(join(dir,'summary.json'),JSON.stringify(report));save();action({dir,report,save,payload});
 }finally{rmSync(dir,{recursive:true,force:true});}
}
 test('post-completion truncation of a complete value profile is rejected',()=>evidence(({dir})=>{verifySourceConfigValueArtifacts(dir);writeFileSync(join(dir,'actual-000.jsonl'),'');assert.throws(()=>verifySourceConfigValueArtifacts(dir),/integrity/);}));
 test('empty manifests never certify missing raw evidence',()=>evidence(({dir,report,save})=>{report.artifacts=[];save();assert.throws(()=>verifySourceConfigValueArtifacts(dir));}));
 test('every required file remains required when its manifest entry and payload are deleted',()=>evidence(({dir,report,save,payload})=>{const original=[...report.artifacts];for(const item of original){report.artifacts=original.filter(a=>a.path!==item.path);unlinkSync(join(dir,item.path));save();assert.throws(()=>verifySourceConfigValueArtifacts(dir),item.path);writeFileSync(join(dir,item.path),payload[item.path]);}report.artifacts=original;save();verifySourceConfigValueArtifacts(dir);}));
 test('shard counts cannot be reduced through a rehashed calls manifest',()=>evidence(({dir,report,save})=>{writeFileSync(join(dir,'actual-calls.json'),'[]');const a=report.artifacts.find(a=>a.path==='actual-calls.json');a.bytes=2;a.sha256=sha256('[]');save();assert.throws(()=>verifySourceConfigValueArtifacts(dir));}));
 test('rehashed shard identities cannot diverge from selected config IDs',()=>evidence(({dir,report,save,payload})=>{const rows=payload['actual-000.jsonl'].trim().split('\n').map(JSON.parse);rows[1].id=rows[0].id;const body=rows.map(r=>JSON.stringify(r)).join('\n')+'\n';writeFileSync(join(dir,'actual-000.jsonl'),body);const a=report.artifacts.find(a=>a.path==='actual-000.jsonl');a.bytes=Buffer.byteLength(body);a.sha256=sha256(body);const calls=JSON.parse(payload['actual-calls.json']);calls[0].stdoutSha256=a.sha256;const cb=JSON.stringify(calls);writeFileSync(join(dir,'actual-calls.json'),cb);const c=report.artifacts.find(a=>a.path==='actual-calls.json');c.bytes=Buffer.byteLength(cb);c.sha256=sha256(cb);save();assert.throws(()=>verifySourceConfigValueArtifacts(dir));}));

test('claimed report totals must reconcile to raw config outcomes',()=>evidence(({dir,report,save})=>{report.counts.MATCH=999;save();assert.throws(()=>verifySourceConfigValueArtifacts(dir));}));

function rehash(e){
 const oracle=JSON.parse(e.payload['oracle/summary.json']);for(const a of oracle.artifacts){const body=e.payload['oracle/'+a.path];a.bytes=Buffer.byteLength(body);a.sha256=sha256(body);}e.payload['oracle/summary.json']=JSON.stringify(oracle);
 for(const a of e.report.artifacts){const body=e.payload[a.path];writeFileSync(join(e.dir,a.path),body);a.bytes=Buffer.byteLength(body);a.sha256=sha256(body);}e.save();
}
test('rehashed reference echoes still bind to exact parse requests',()=>{for(const field of ['id','sourceBytes','fileName'])evidence(e=>{const refs=JSON.parse(e.payload['oracle/reference.json']),g=JSON.parse(e.payload['golden.json']);refs[0][field]='foreign';g.vectors[0].reference=refs[0];e.payload['oracle/reference.json']=JSON.stringify(refs);e.payload['golden.json']=JSON.stringify(g);e.payload['results.jsonl']=reconcileSourceConfigValues(g.vectors,g.vectors.map(actual)).map(r=>JSON.stringify(r)).join('\n')+'\n';rehash(e);assert.throws(()=>verifySourceConfigValueArtifacts(e.dir));});});
test('derived diagnostic and selected-skip counters cannot be forged',()=>{for(const field of ['parserErrorConfigs','parserDiagnostics','conversionDiagnostics','selectedUpstreamSkipped'])evidence(e=>{e.report.counts[field]=999;e.save();assert.throws(()=>verifySourceConfigValueArtifacts(e.dir));});});
test('shared source-scope counters cannot disagree between reports',()=>{for(const field of ['upstreamSkippedSources','nonRawUtf8Sources'])evidence(e=>{e.report.counts[field]=999;e.save();assert.throws(()=>verifySourceConfigValueArtifacts(e.dir));});});
