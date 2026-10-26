import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {pin, checkBytes, buildCatalogue, renderCSharp} from './upstream-placeholders.ts';
const id = (kind, ch) => `${kind}:${ch.repeat(64)}`;
const source = () => ({sourceId:id('fixture','1'),path:'tsc/testdata/tests/cases/compiler/example.ts',rawSha256:'2'.repeat(64),upstreamSelection:{status:'UPSTREAM_SKIPPED'},id:id('source-configuration-profile','3'),profile:'compiler/source-settings/configurations',status:'MATCH',equal:true,actualOutcome:'RESULT',referenceOutcome:'RESULT',configurations:[{id:id('configuration','4'),index:0,configuration:{Name:'target=es2015',Config:{target:'es2015'}},compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'},{id:id('configuration','5'),index:1,configuration:null,compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'}],compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'});
test('enumerates every configuration with stable source, settings and skip metadata',()=>{
 const r=source(); const c=buildCatalogue([r],pin);
 assert.equal(c.targets.length,2); assert.equal(c.summary.configurationTargets,2);
 assert.equal(c.summary.skippedUnimplemented,2); assert.equal(c.summary.accepted,0);
 assert.deepEqual(c.targets.map(x=>x.id),r.configurations.map(x=>x.id));
 for(const [i,t] of c.targets.entries()){
  assert.equal(t.sourceId,r.sourceId);assert.equal(t.upstreamPath,r.path);assert.equal(t.rawSha256,r.rawSha256);
  assert.deepEqual(t.configuration,r.configurations[i].configuration);assert.deepEqual(t.upstreamSelection,r.upstreamSelection);
  assert.equal(t.status,'SKIP');assert.equal(t.reason,'UNIMPLEMENTED');assert.equal(t.compiler,'NOT_RUN');
  assert.equal(t.missingCapability,'ORIGINAL_COMPILER_RUNNER');assert.equal(t.productFeatureImplementation,'UNASSESSED');
 }
});
test('reference-fatal sources remain separate blockers without fabricated configurations',()=>{
 const a=source(), b=source(); b.sourceId=id('fixture','6');b.id=id('source-configuration-profile','7');b.path=b.path.replace('example','blocked');b.status='REFERENCE_FATAL';b.referenceOutcome='FATAL';b.actualOutcome='FATAL';b.configurations=null;
 const c=buildCatalogue([a,b],pin);assert.equal(c.targets.length,2);assert.equal(c.blocked.length,1);assert.equal(c.blocked[0].status,'REFERENCE_FATAL');assert.equal(c.summary.sources,2);assert.equal(c.summary.blockedSources,1);assert.equal(c.summary.releaseEligible,false);
});
test('rejects duplicate sources, duplicate configurations and empty scope',()=>{
 assert.throws(()=>buildCatalogue([],pin),/empty/i);
 assert.throws(()=>buildCatalogue([source(),source()],pin),/duplicate/i);
 const r=source();r.configurations[1].id=r.configurations[0].id;assert.throws(()=>buildCatalogue([r],pin),/duplicate/i);
});
test('rejects wrong pin, unsafe paths, broken identities and lost configuration lists',()=>{
 assert.throws(()=>buildCatalogue([source()],'a'.repeat(40)),/pin/i);
 for(const mutate of [r=>r.path='../bad.ts',r=>r.rawSha256='bad',r=>r.configurations=[],r=>r.configurations[0].index=9,r=>r.configurations[0].configuration.Config.target=1]){
  const r=source();mutate(r);assert.throws(()=>buildCatalogue([r],pin));
 }
});
test('never downgrades prior compiler passes, failures or missing statuses to SKIP',()=>{
 for(const status of ['PASS','FAIL','CRASH','TIMEOUT',undefined]){
  const r=source();r.acceptance=status;assert.throws(()=>buildCatalogue([r],pin),/acceptance|execution|NOT_RUN/i);
  const s=source();s.configurations[0].compiler=status;assert.throws(()=>buildCatalogue([s],pin),/acceptance|execution|NOT_RUN/i);
 }
});
test('hash and length binding fail closed on changed or truncated input',()=>{
 const b=Buffer.from('ledger\n'),e={bytes:b.length,sha256:createHash('sha256').update(b).digest('hex')};
 assert.doesNotThrow(()=>checkBytes(b,e));assert.throws(()=>checkBytes(Buffer.from('Ledger\n'),e),/hash|digest/i);assert.throws(()=>checkBytes(b.subarray(1),e),/length|size/i);
});
test('C# placeholders are named SKIPs and throw if enabled without implementation',()=>{
 const t=buildCatalogue([source()],pin).targets;const code=renderCSharp(t);
 assert.equal((code.match(/\[Fact\(/g)||[]).length,2);assert.equal((code.match(/Skip = /g)||[]).length,2);
 assert.equal((code.match(/throw new NotImplementedException/g)||[]).length,2);
 assert.ok(code.includes('ORIGINAL_COMPILER_RUNNER'));assert.ok(code.includes('example.ts'));
 assert.ok(code.includes(id('configuration','4')));assert.equal(code.includes('Assert.True(true)'),false);
});
test('transpile targets have their own missing runner and original configuration',()=>{
 const r=source();r.path=r.path.replace('/compiler/','/transpile/');r.profile='transpile/source-settings/configurations';
 const c=buildCatalogue([r],pin);assert.equal(c.summary.byRunner.transpile,2);assert.equal(c.targets[0].missingCapability,'ORIGINAL_TRANSPILE_RUNNER');
});
