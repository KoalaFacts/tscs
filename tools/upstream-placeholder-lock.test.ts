import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {projectPlaceholderLock,verifyPlaceholderLock} from './upstream-placeholder-lock.ts';

const original={version:1,dependencies:{'net10.0':{
 'xunit.v3.mtp-v2':{type:'Direct',requested:'[4.0.2, )',resolved:'4.0.2',contentHash:'hash',dependencies:{'xunit.v3.assert':'[4.0.2]'}},
 'xunit.v3.assert':{type:'Transitive',resolved:'4.0.2',contentHash:'otherhash'},
 'tscs':{type:'Project',dependencies:{'Tscs.Compiler':'[1.0.0, )'}}
}}};

test('projects only pinned xunit and its transitive package hashes, without project references',()=>{
 const locked=projectPlaceholderLock(original);
 assert.deepEqual(Object.keys(locked.dependencies['net10.0']),['xunit.v3.mtp-v2','xunit.v3.assert']);
 assert.deepEqual(locked.dependencies['net10.0']['xunit.v3.mtp-v2'],original.dependencies['net10.0']['xunit.v3.mtp-v2']);
 assert.doesNotThrow(()=>verifyPlaceholderLock(original,locked));
});
test('rejects changed version or added direct dependency',()=>{
 const c=structuredClone(original);c.dependencies['net10.0']['xunit.v3.mtp-v2'].resolved='4.0.3';
 assert.throws(()=>projectPlaceholderLock(c),/xunit.*4\.0\.2/i);
 const d=structuredClone(original);d.dependencies['net10.0']['xunit.v3.assert'].type='Direct';
 assert.throws(()=>projectPlaceholderLock(d),/direct/i);
});
test('rejects missing, altered or extra hashes in candidate lock',()=>{
 const candidate=projectPlaceholderLock(original);
 const missing=structuredClone(candidate);delete missing.dependencies['net10.0']['xunit.v3.assert'];
 assert.throws(()=>verifyPlaceholderLock(original,missing),/lock.*mismatch/i);
 const altered=structuredClone(candidate);altered.dependencies['net10.0']['xunit.v3.assert'].contentHash='wrong';
 assert.throws(()=>verifyPlaceholderLock(original,altered),/lock.*mismatch/i);
 const additional=structuredClone(candidate);additional.dependencies['net10.0']['extra']={type:'Transitive',resolved:'1.0.0',contentHash:'extra'};
 assert.throws(()=>verifyPlaceholderLock(original,additional),/lock.*mismatch/i);
});
test('rejects project dependencies, unknown frameworks and missing hash',()=>{
 const c=structuredClone(original);c.dependencies['net10.0']['xunit.v3.assert'].contentHash='';
 assert.throws(()=>projectPlaceholderLock(c),/contentHash/i);
 const d=structuredClone(original);d.dependencies['net9.0']=d.dependencies['net10.0'];
 assert.throws(()=>projectPlaceholderLock(d),/net10\.0/i);
 const e=projectPlaceholderLock(original);e.dependencies['net10.0']['tscs']={type:'Project'};
 assert.throws(()=>verifyPlaceholderLock(original,e),/lock.*mismatch/i);
});
test('checked-in standalone lock matches existing SDK-pinned real project graph',()=>{
 const source=JSON.parse(readFileSync(resolve('tests/Tscs.Compiler.Tests/packages.lock.json'),'utf8'));
 const candidate=JSON.parse(readFileSync(resolve('tests/Tscs.Upstream.DiscoveryProbe/packages.lock.json'),'utf8'));
 assert.doesNotThrow(()=>verifyPlaceholderLock(source,candidate));
});
