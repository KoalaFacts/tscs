import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {sha256,validateProgress} from './ts7-scope.ts';

const read=path=>JSON.parse(readFileSync(new URL(`../${path}`,import.meta.url)));

test('checked-in scope summary binds the actual generator, upstream pin and progress bytes',()=>{
  const summary=read('docs/testing/ts7/inventory-summary.json'), lock=read('upstream.lock.json');
  assert.equal(summary.provenance.commit,lock.commit);
  for(const item of summary.provenance.generatorFiles) assert.equal(sha256(readFileSync(new URL(`../${item.path}`,import.meta.url))),item.sha256,`Stale scope summary: ${item.path}`);
  const progressBytes=readFileSync(new URL('../docs/testing/ts7/progress.json',import.meta.url));
  const progressArtifact=summary.artifacts.find(x=>x.path==='progress.json');
  assert.equal(progressBytes.length,progressArtifact.bytes);
  assert.equal(sha256(progressBytes),progressArtifact.sha256);
  assert.equal(summary.reconciliation.recordedAssets,summary.reconciliation.gitTreeEntries);
  assert.equal(summary.counts.domains.reduce((n,x)=>n+x.assets,0),summary.counts.trackedAssets);
});

test('initial source-scope progress cannot silently become accepted coverage',()=>{
  const progress=read('docs/testing/ts7/progress.json');
  assert.equal(progress.scopeComplete,false);assert.equal(progress.releaseEligible,false);assert.equal(progress.concreteCases,null);
  assert.deepEqual(progress.frozenCohort,{separate:true,cases:29,mappingToUpstreamInventory:'UNVERIFIED',creditTowardFullScope:0});
  for(const domain of progress.domains) {
    assert.equal(validateProgress(domain),true);
    assert.equal(domain.expansion,'UNEXPANDED');assert.equal(domain.implementation,'UNIMPLEMENTED');
    for(const key of ['compiler','reference','acceptance'])assert.equal(domain[key],'NOT_RUN');
    assert.deepEqual(domain.evidence,[]);
  }
});
