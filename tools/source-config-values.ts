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
 const root=resolve(directory),report=JSON.parse(readFileSync(resolve(root,'summary.json'),'utf8'));
 if(!Array.isArray(report.artifacts)||new Set(report.artifacts.map(a=>a.path)).size!==report.artifacts.length)throw Error('Invalid artifact manifest');
 for(const a of report.artifacts){const path=resolve(root,a.path);if(!path.startsWith(root+sep))throw Error('Artifact path escape');const bytes=readFileSync(path);if(bytes.length!==a.bytes||sha256(bytes)!==a.sha256)throw Error('Artifact integrity mismatch: '+a.path);}
 return report;
}
