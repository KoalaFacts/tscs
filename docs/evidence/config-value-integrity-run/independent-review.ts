import {readFileSync,writeFileSync,cpSync,rmSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {verifySourceConfigValueArtifacts,reconcileSourceConfigValues} from '/workspace/scratch/daf3e6c2e318/tscs/tools/source-config-values.ts';
const profiles=['/workspace/scratch/daf3e6c2e318/tscs/.artifacts/source-config-values-verified','/workspace/scratch/daf3e6c2e318/tscs/.artifacts/source-config-values-verified-repeat','/tmp/tscs-source-values-review/profile'];
for(const p of profiles){const r=verifySourceConfigValueArtifacts(p);console.log('historical PASS',p,JSON.stringify(r.counts));}
const dir='/tmp/tscs-integrity-review/mutation-profile';rmSync(dir,{recursive:true,force:true});cpSync(profiles[0],dir,{recursive:true});
const read=p=>readFileSync(dir+'/'+p),json=p=>JSON.parse(read(p)),save=(p,v)=>writeFileSync(dir+'/'+p,JSON.stringify(v));
const hash=p=>createHash('sha256').update(read(p)).digest('hex');
function rehash(){const nested=json('oracle/summary.json');for(const a of nested.artifacts){const p='oracle/'+a.path;a.bytes=read(p).length;a.sha256=hash(p);}save('oracle/summary.json',nested);const report=json('summary.json');for(const a of report.artifacts){a.bytes=read(a.path).length;a.sha256=hash(a.path);}save('summary.json',report);}
function attempt(name){try{verifySourceConfigValueArtifacts(dir);console.log('MUTATION ACCEPTED',name);}catch(e){console.log('MUTATION REJECTED',name,e.message);}}
const base=read('summary.json');
for(const key of ['parserErrorConfigs','parserDiagnostics','conversionDiagnostics','selectedUpstreamSkipped','upstreamSkippedSources','nonRawUtf8Sources']){const r=JSON.parse(base);r.counts[key]=999;save('summary.json',r);attempt('report.counts.'+key+'=999');}
writeFileSync(dir+'/summary.json',base);
const golden=json('golden.json'),reference=json('oracle/reference.json');reference[0].id='foreign-reference';reference[0].sourceBytes='d3Jvbmc=';reference[0].fileName='L3dyb25nLmpzb24=';golden.vectors[0].reference=reference[0];save('oracle/reference.json',reference);save('golden.json',golden);
const calls=json('actual-calls.json'),actual=calls.flatMap(c=>read(c.artifact).toString('utf8').trimEnd().split('\n').filter(Boolean).map(l=>JSON.parse(l)));
writeFileSync(dir+'/results.jsonl',reconcileSourceConfigValues(golden.vectors,actual).map(r=>JSON.stringify(r)).join('\n')+'\n');rehash();attempt('foreign reference id/content/path with golden/results rehashed');
