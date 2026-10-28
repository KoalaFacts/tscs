import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {buildCoverage,classifyImplementation,extractInlineSources} from './implementation-coverage.ts';
const registry=JSON.parse(readFileSync('tests/implementation-coverage/registry.json','utf8'));
const source=readFileSync('tests/Tscs.Compiler.Tests/CompilationTests.cs');
const lock=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const copy=()=>structuredClone(registry);
test('every known missing guard remains an explicit local skip, not TS7 acceptance',()=>{
 const r=buildCoverage(registry,source,lock);
 assert.equal(r.counts.registered,14);
 assert.equal(r.counts.skippedUnimplemented,5);
 assert.equal(r.counts.implemented,9);
 assert.equal(r.counts.verifiedUpstream,0);
 assert.equal(r.concreteUpstreamCases,null);
 assert.equal(r.releaseEligible,false);
 assert.equal(r.uniqueUnimplementedCapabilities.length,2);
 assert.equal(r.counts.registered,r.counts.implemented+r.counts.skippedUnimplemented);
 const moved=r.cases.find(x=>x.id==='bootstrap:postfix-update');
 assert.equal(moved?.capability,'numeric-postfix-update');
 assert.equal(moved?.implementation,'IMPLEMENTED');
 assert.equal(moved?.status,'NOT_RUN');
 assert.equal(moved?.upstreamExecutableCaseId,null);
 const object=r.cases.find(x=>x.id==='bootstrap:object-literal');
 assert.equal(object?.implementation,'IMPLEMENTED');
 assert.equal(object?.status,'NOT_RUN');
 assert.equal(object?.upstreamExecutableCaseId,null);
});
test('every local guard row must have its own registered skip or implementation classification',()=>{
 const smaller=copy();smaller.cases.splice(4,1);
 assert.throws(()=>buildCoverage(smaller,source,lock),/unregistered/);
});
test('a guard may not be reclassified as implemented by changing its label',()=>{
 const wrong=copy();wrong.cases[5].implementation='IMPLEMENTED';wrong.cases[5].skipReason=null;
 assert.throws(()=>buildCoverage(wrong,source,lock),/unsupported guard classification/);
});
test('positive tests must not be reclassified as skips',()=>{
 const wrong=copy();wrong.cases[0].implementation='UNIMPLEMENTED';wrong.cases[0].skipReason='UNIMPLEMENTED';wrong.cases[0].implementationLocation=null;
 assert.throws(()=>buildCoverage(wrong,source,lock),/unsupported guard classification/);
});
test('stale source, changed pin and duplicate identity fail closed',()=>{
 assert.throws(()=>buildCoverage(registry,Buffer.from(source.toString()+'\n'),lock),/blob mismatch/);
 const otherPin={...lock,commit:'0'.repeat(40)};
 assert.throws(()=>buildCoverage(registry,source,otherPin),/pin mismatch/);
 const dup=copy();dup.cases[5].id=dup.cases[4].id;
 assert.throws(()=>buildCoverage(dup,source,lock),/duplicate/);
});
test('a recognized regression failure remains a FAIL and cannot become a SKIP',()=>{
 const implemented=registry.cases[0];
 assert.deepEqual(classifyImplementation(implemented,'FAIL'),{status:'FAIL',reason:null,executed:true});
 assert.equal(classifyImplementation(implemented,'CRASH').status,'CRASH');
 assert.equal(classifyImplementation(implemented,'TIMEOUT').status,'TIMEOUT');
 assert.equal(classifyImplementation(implemented).status,'NOT_RUN');
 assert.throws(()=>classifyImplementation(registry.cases[5],'FAIL'),/invalid unimplemented skip/);
});
test('guard input parser retains exact C# inline string escapes and row counts',()=>{
 const text=source.toString('utf8');
 assert.equal(extractInlineSources(text,'ValidPrimitiveCodeHasNoErrors').length,9);
 assert.equal(extractInlineSources(text,'UnsupportedSyntaxNeverSucceeds').length,1);
 assert.equal(extractInlineSources(text,'IntrinsicGlobalThisIsExplicitlyUnsupported').length,4);
 assert.equal(extractInlineSources(text,'ValidPrimitiveCodeHasNoErrors')[7], '// @ts-ignore\nlet n: number = "x";');
 assert.equal(extractInlineSources(text,'ValidPrimitiveCodeHasNoErrors')[4], 'let n: any = 1;');
 assert.equal(extractInlineSources(text,'ValidPrimitiveCodeHasNoErrors')[5], 'let n = 1; n++;');
});
