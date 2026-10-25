import {isDeepStrictEqual} from 'node:util';
import {readFileSync} from 'node:fs';
import {resolve,sep} from 'node:path';
import {sha256} from './ts7-scope.ts';
// Selected configs only. Selection over the whole source scope is a separate
// actual-Go step; no-config sources receive no value/compiler acceptance credit.
export function reconcileSourceConfigValues(vectors,actual){
 if(actual.length!==vectors.length||new Set(actual.map(r=>r.id)).size!==actual.length)throw Error('Actual row omission/duplicate');
 return vectors.map((v,i)=>{
  const a=actual[i];if(a.id!==v.id)throw Error('Actual identity/order mismatch');
  const p=a.expected?.preparation,r=v.reference;
  if(!p||!Array.isArray(a.expected.conversionDiagnostics))throw Error('Malformed value result');
  const outcome=a.expected.outcome;
  if(!['VALUE','VALUE_DIAGNOSTICS','UNSUPPORTED','PREPARATION_PANIC','NOT_APPLICABLE'].includes(outcome))throw Error('Unknown value outcome');
  const equal=isDeepStrictEqual(p.selection,v.selection)&&isDeepStrictEqual(p.context,v.context)&&p.unsupported===null&&p.ast?.unsupported===null&&p.ast?.panic===null&&a.expected.unsupported===null&&isDeepStrictEqual(p.ast?.diagnostics,r.parserDiagnostics)&&isDeepStrictEqual(a.expected.conversionDiagnostics,r.conversionDiagnostics)&&isDeepStrictEqual(a.expected.value,r.value);
  const diagnostics=r.parserDiagnostics.length+r.conversionDiagnostics.length;
  const expectedOutcome=diagnostics?'VALUE_DIAGNOSTICS':'VALUE';
  const status=outcome==='PREPARATION_PANIC'?'CRASH':outcome==='UNSUPPORTED'?'UNSUPPORTED':equal&&outcome===expectedOutcome?(diagnostics?'MATCH_DIAGNOSTICS':'MATCH'):'MISMATCH';
  return {id:v.id,configId:v.configId,source:v.source,status,equal:equal&&outcome===expectedOutcome,actualOutcome:outcome,referenceOutcome:expectedOutcome,actualSha256:sha256(JSON.stringify(a)),referenceSha256:sha256(JSON.stringify(r)),compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'};
 });
}
export function verifySourceConfigValueArtifacts(directory){
 const root=resolve(directory),read=path=>readFileSync(resolve(root,path)),json=path=>JSON.parse(read(path).toString('utf8'));
 const report=json('summary.json');
 const fail=message=>{throw Error('Invalid config value evidence: '+message);};
 const fixed=['golden.json','generate.log','build.log','actual-calls.json','results.jsonl','oracle/summary.json','oracle/requests.json','oracle/selection.json','oracle/selection.log','oracle/parse-requests.json','oracle/reference.json','oracle/reference.log'];
 const checkManifest=(artifacts,required)=>{
  if(!Array.isArray(artifacts)||!artifacts.length||new Set(artifacts.map(a=>a.path)).size!==artifacts.length)fail('empty or duplicate manifest');
  if(required.some(p=>!artifacts.some(a=>a.path===p)))fail('required artifact omitted');
  for(const a of artifacts){
   if(typeof a.path!=='string'||!Number.isSafeInteger(a.bytes)||a.bytes<0||!(/^[a-f0-9]{64}$/).test(a.sha256))fail('artifact schema');
   const path=resolve(root,a.path);if(!path.startsWith(root+sep))fail('artifact path escape');const bytes=read(a.path);
   if(bytes.length!==a.bytes||sha256(bytes)!==a.sha256)throw Error('Artifact integrity mismatch: '+a.path);
  }
 };
 checkManifest(report.artifacts,fixed);
 const golden=json('golden.json'),vectors=golden.vectors,calls=json('actual-calls.json');
 if(report.profile!=='selected-config-values/default-options/pre-vfs/no-notifier'||report.upstreamCommit!=='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f'||golden.profile!==report.profile||golden.upstreamCommit!==report.upstreamCommit)fail('profile/pin');
 const n=report.counts?.selectedConfigs;
 if(!Number.isSafeInteger(n)||n<=0||report.counts.actualValueConfigs!==n||golden.counts?.selectedConfigs!==n||!Array.isArray(vectors)||vectors.length!==n||new Set(vectors.map(v=>v.id)).size!==n)fail('selected config count/identity');
 if(!Array.isArray(calls)||calls.length!==Math.ceil(n/32))fail('actual batch coverage');
 const actual=[],shards=[];
 const jsonl=path=>read(path).toString('utf8').trimEnd().split('\n').filter(Boolean).map(l=>JSON.parse(l));
 for(let i=0;i<calls.length;i++){
  const c=calls[i],name='actual-'+String(i).padStart(3,'0')+'.jsonl',offset=i*32,size=Math.min(32,n-offset);
  if(c.artifact!==name||c.offset!==offset||c.requests!==size||c.status!==0)fail('actual batch binding');
  shards.push(name,name+'.log');
  if(!report.artifacts.some(a=>a.path===name)||!report.artifacts.some(a=>a.path===name+'.log'))fail('actual shard omitted');
  if(c.stdoutSha256!==sha256(read(name)))fail('actual stdout binding');
  const rows=jsonl(name);if(rows.length!==size||rows.some((r,j)=>r.id!==vectors[offset+j].id))fail('actual row identity/count');actual.push(...rows);
 }
 const expected=new Set([...fixed,...shards]);if(report.artifacts.length!==expected.size||report.artifacts.some(a=>!expected.has(a.path)))fail('unexpected artifact set');
 const oracle=json('oracle/summary.json'),oracleFiles=fixed.filter(p=>p.startsWith('oracle/')&&p!=='oracle/summary.json');
 // Nested producer manifest must also enumerate exactly its complete six files.
 const nested=oracle.artifacts?.map(a=>({...a,path:'oracle/'+a.path}));checkManifest(nested,oracleFiles);
 if(nested.length!==oracleFiles.length||nested.some(a=>!oracleFiles.includes(a.path)))fail('oracle artifact set');
 const requests=json('oracle/requests.json'),selection=json('oracle/selection.json'),reference=json('oracle/reference.json'),parseRequests=json('oracle/parse-requests.json');
 if(!Array.isArray(requests)||requests.length!==report.counts.sources||selection.length!==requests.length||new Set(requests.map(r=>r.id)).size!==requests.length||golden.counts.sources!==requests.length||oracle.counts?.selectedConfigs!==n)fail('source scope');
 for(let i=0;i<requests.length;i++){const q=requests[i],s=selection[i];if(q.id!==s.id||q.input!==s.input||q.fileName!==s.fileName||s.expected?.panic!==null)fail('selection binding');}
 const selected=selection.filter(s=>s.expected.configFile!==null);
 if(selected.length!==n||parseRequests.length!==n||reference.length!==n)fail('reference selection coverage');
 for(let i=0;i<n;i++){
  const v=vectors[i],s=selected[i],q=requests.find(r=>r.id===v.id);
  if(reference[i]?.id!==parseRequests[i]?.id||reference[i]?.sourceBytes!==parseRequests[i]?.sourceBytes||reference[i]?.fileName!==parseRequests[i]?.fileName)fail('reference input echo');
  if(v.id!==s.id||!isDeepStrictEqual(v.request,q)||!isDeepStrictEqual(v.selection,s.expected)||!isDeepStrictEqual(v.context,s.context)||!isDeepStrictEqual(v.reference,reference[i])||!isDeepStrictEqual(parseRequests[i],{id:v.id,sourceBytes:s.expected.configFile.content,fileName:s.context.configPath}))fail('selected reference binding');
 }
 // Full-source skip/encoding totals are generation metadata, not reconstructed
 // from selected-only vectors. Require agreement across all three reports.
 for(const key of Object.keys(golden.counts))if(!isDeepStrictEqual(report.counts[key],golden.counts[key])||!isDeepStrictEqual(oracle.counts[key],golden.counts[key]))fail('shared scope metadata');
 const derived={parserErrorConfigs:reference.filter(r=>r.parserDiagnostics.length).length,parserDiagnostics:reference.reduce((n,r)=>n+r.parserDiagnostics.length,0),conversionDiagnostics:reference.reduce((n,r)=>n+r.conversionDiagnostics.length,0),selectedUpstreamSkipped:vectors.filter(v=>v.source.upstreamSelection.status==='UPSTREAM_SKIPPED').length};
 for(const [key,value] of Object.entries(derived))if(report.counts[key]!==value)fail('derived counter');
 const results=reconcileSourceConfigValues(vectors,actual);
 if(!isDeepStrictEqual(jsonl('results.jsonl'),results))fail('result reconciliation');
 for(const status of ['MATCH','MATCH_DIAGNOSTICS','MISMATCH','UNSUPPORTED','CRASH'])if(report.counts[status]!==results.filter(r=>r.status===status).length)fail('outcome totals');
 if(report.counts.noConfigSources!==requests.length-n||golden.counts.noConfigSources!==requests.length-n)fail('no-config totals');
 return report;
}
