import fs from 'node:fs';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const base='/workspace/scratch/daf3e6c2e318/tscs';
const dirs=[base+'/.artifacts/source-config-values-verified',base+'/.artifacts/source-config-values-verified-repeat','/tmp/tscs-source-values-review/profile'];
let count=0;
for(const dir of dirs){const s=JSON.parse(fs.readFileSync(dir+'/summary.json'));for(const a of s.artifacts){const b=fs.readFileSync(dir+'/'+a.path);assert.equal(b.length,a.bytes);assert.equal(sha(b),a.sha256);count++;}for(const [p,h] of Object.entries(s.sourceHashes))assert.equal(sha(fs.readFileSync(base+'/'+p)),h,p);console.log(dir+': '+s.artifacts.length+' manifest entries and '+Object.keys(s.sourceHashes).length+' source hashes valid');}
const payloads=['golden.json','actual-calls.json','results.jsonl','oracle/requests.json','oracle/selection.json','oracle/parse-requests.json','oracle/reference.json',...Array.from({length:6},(_,i)=>'actual-'+String(i).padStart(3,'0')+'.jsonl')];
for(const p of payloads)for(const dir of dirs.slice(1))assert.deepEqual(fs.readFileSync(dirs[0]+'/'+p),fs.readFileSync(dir+'/'+p),p);
const golden=fs.readFileSync(base+'/tests/Tscs.Compiler.Tests/TestData/source-config-values.json');
assert.deepEqual(golden,fs.readFileSync(dirs[2]+'/golden.json'));
assert.equal(sha(golden),'a42df34c1b72c3e42f12f19c7e6894f375afdf63f72c34429f02dbd59e492ae9');
const g=JSON.parse(golden);const sel=JSON.parse(fs.readFileSync(dirs[2]+'/oracle/selection.json'));const req=JSON.parse(fs.readFileSync(dirs[2]+'/oracle/requests.json'));const inventory=fs.readFileSync(base+'/.artifacts/ts7-scope/fixtures.jsonl','utf8').trimEnd().split('\n').map(JSON.parse);
assert.equal(sel.length,13006);assert.equal(req.length,13006);assert.equal(new Set(req.map(x=>x.id)).size,13006);
for(let i=0;i<req.length;i++){assert.equal(req[i].id,sel[i].id);assert.equal(req[i].input,sel[i].input);assert.equal(req[i].fileName,sel[i].fileName);}
assert.equal(sel.filter(x=>x.expected.configFile===null).length,12834);
for(const v of g.vectors){const source=inventory.find(x=>x.id===v.source.sourceId);assert.equal(v.source.path,source.path);assert.equal(v.source.rawSha256,source.rawSha256);assert.equal(v.source.decoding,source.decoding);assert.deepEqual(v.source.upstreamSelection,source.upstreamSelection);}
console.log('All '+count+' artifact entries valid; '+payloads.length+' nonlog payloads identical across all three runs; committed golden identical '+sha(golden));
console.log(JSON.stringify(g.counts));console.log('Syntax-error source: '+g.vectors.find(x=>x.reference.parserDiagnostics.length).source.path);
