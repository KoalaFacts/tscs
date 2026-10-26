import test from 'node:test';
import assert from 'node:assert/strict';
import {reconcileUpstreamDiscovery} from './verify-upstream-discovery.ts';

const id=(x: string)=>'configuration:'+x.repeat(64);
const targets=[
 {id:id('a'),status:'SKIP',reason:'UNIMPLEMENTED',upstreamPath:'tsc/testdata/tests/cases/compiler/a.ts',missingCapability:'ORIGINAL_COMPILER_RUNNER',upstreamSelection:{status:'SELECTED_BY_EXTENSION'}},
 {id:id('b'),status:'SKIP',reason:'UNIMPLEMENTED',upstreamPath:'tsc/testdata/tests/cases/compiler/b.ts',missingCapability:'ORIGINAL_COMPILER_RUNNER',upstreamSelection:{status:'UPSTREAM_SKIPPED'}},
];
const report=(tests:object[],summary:object={})=>({results:{tool:{name:'xUnit.net'},summary:{tests:tests.length,passed:0,failed:0,skipped:tests.length,pending:0,other:0,...summary},tests}});
const entry=(t:typeof targets[number],status='skipped')=>({name:t.upstreamPath+' [default] '+t.id,status});

test('CTRF discovery accounts for both explicit unimplemented targets, including upstream-skip metadata',()=>{
 const result=reconcileUpstreamDiscovery(targets,report(targets.map(t=>entry(t))),{expected:2});
 assert.deepEqual(result,{registered:2,discovered:2,skippedUnimplemented:2,upstreamSkippedMetadata:1,passed:0,failed:0,notRun:0,releaseEligible:false});
});
test('missing discovered Fact fails rather than decreasing target denominator',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([entry(targets[0])]),{expected:2}),/missing|count|discover/i);
});
test('duplicate discovered Fact fails rather than counting it twice',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([entry(targets[0]),entry(targets[0])]),{expected:2}),/duplicate|identity/i);
});
test('unexpected Fact fails rather than silently extending scope',()=>{
 const extra={...targets[0],id:id('c')};
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([...targets.map(t=>entry(t)),entry(extra)]),{expected:2}),/unknown|unregistered|unexpected|count/i);
});
test('runtime passing a placeholder is not credited as completed',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([entry(targets[0],'passed'),entry(targets[1])],{passed:1,skipped:1}),{expected:2}),/pass|skip|unimplemented/i);
});
test('a runtime FAIL is retained and must block',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([entry(targets[0],'failed'),entry(targets[1])],{failed:1,skipped:1}),{expected:2}),/failed|fail|skip/i);
});
test('summary forgery, pending tests or negative counts fail',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report(targets.map(t=>entry(t)),{skipped:1,passed:1}),{expected:2}),/summary|count|skip/i);
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report(targets.map(t=>entry(t)),{pending:1}),{expected:2}),/pending|summary|count/i);
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report(targets.map(t=>entry(t)),{skipped:-1}),{expected:2}),/count|summary|skip/i);
});
test('a test without stable configuration identity cannot masquerade as discovered',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([{name:'UnitTest1',status:'skipped'},entry(targets[1])]),{expected:2}),/identity|name|unknown/i);
});
test('release mode remains blocked even when every placeholder has been discovered and skipped',()=>{
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report(targets.map(t=>entry(t))),{expected:2,release:true}),/release|unimplemented|skip/i);
});
test('complete future implemented PASS remains a mismatch until original assertions are separately supplied',()=>{
 const modified=targets.map(t=>({...t,status:'PASS',reason:null}));
 assert.throws(()=>reconcileUpstreamDiscovery(modified,report(targets.map(t=>entry(t))),{expected:2}),/unexpected target implementation status|only explicit skip|cannot infer implementation/i);
});

test('CTRF name must retain exact upstream source/configuration binding, not just an existing ID',()=>{
 const forged={...entry(targets[0]),name:'tsc/testdata/tests/cases/compiler/other.ts [default] '+targets[0].id};
 assert.throws(()=>reconcileUpstreamDiscovery(targets,report([forged,entry(targets[1])]),{expected:2}),/name|path|source|binding/i);
});
