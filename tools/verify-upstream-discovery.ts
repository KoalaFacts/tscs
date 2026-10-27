// Fail-closed reconciliation of actual xUnit 4 / MTP 2 CTRF execution against
// source-bound configuration placeholders. Not original compiler acceptance.
import {readFileSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {pathToFileURL} from 'node:url';

const demand=(condition,message)=>{if(!condition)throw Error(message);};
const isRecord=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
const isCount=x=>Number.isSafeInteger(x)&&x>=0;
const targetPattern=/^configuration:[a-f0-9]{64}$/;
const identityPattern=/configuration:[a-f0-9]{64}/g;
const methodPattern=/Target_([a-f0-9]{64})/g;

export function reconcileUpstreamDiscovery(targets,report,{expected=15523,release=false}={}){
 demand(isCount(expected)&&expected>0,'Explicit fixed nonzero denominator required');
 demand(Array.isArray(targets)&&targets.length===expected,'Registered target count differs from frozen denominator');
 const registered=new Map();let upstreamSkippedMetadata=0;
 for(const target of targets){
  demand(isRecord(target)&&targetPattern.test(target.id)&&!registered.has(target.id),'Duplicate or invalid registered target identity');
  demand(target.status==='SKIP'&&target.reason==='UNIMPLEMENTED',
   'Unexpected target implementation status: only explicit SKIP/UNIMPLEMENTED is eligible for placeholder discovery');
  demand(typeof target.upstreamPath==='string'&&target.upstreamPath.startsWith('tsc/testdata/tests/cases/'),
   'Target lacks original upstream source path');
  demand(['ORIGINAL_COMPILER_RUNNER','ORIGINAL_TRANSPILE_RUNNER'].includes(target.missingCapability),
   'Unregistered missing runner capability');
  demand(isRecord(target.upstreamSelection)&&['UPSTREAM_SKIPPED','SELECTED_BY_EXTENSION'].includes(target.upstreamSelection.status),
   'Upstream skip metadata is missing or invalid');
  if(target.upstreamSelection.status==='UPSTREAM_SKIPPED')upstreamSkippedMetadata++;
  registered.set(target.id,target);
 }
 demand(isRecord(report)&&isRecord(report.results)&&Array.isArray(report.results.tests)&&
  isRecord(report.results.summary),'Invalid CTRF execution report: missing test records/summary');
 const summary=report.results.summary;
 const fields=['tests','passed','failed','skipped','pending','other'];
 for(const field of fields)demand(isCount(summary[field]),'Invalid CTRF summary count: '+field);
 demand(fields.slice(1).reduce((s,k)=>s+summary[k],0)===summary.tests,
  'CTRF summary counts do not reconcile');
 const discovered=new Set();
 for(const test of report.results.tests){
  demand(isRecord(test)&&typeof test.name==='string'&&typeof test.status==='string','CTRF test missing name/status');
  const matches=[...(test.name.match(identityPattern)??[])];
  const methods=[...test.name.matchAll(methodPattern)].map(m=>'configuration:'+m[1]);
  const identities=new Set([...matches,...methods]);
  demand(identities.size===1,'CTRF test name lacks an unambiguous configuration identity');
  const id=[...identities][0];
  demand(registered.has(id),'Unknown or unregistered discovered configuration identity: '+id);
  const target=registered.get(id);
  const expectedName=target.upstreamPath+' ['+(target.configuration?.Name||'default')+'] '+id;
  demand(test.name.endsWith(expectedName),'Discovered xUnit name lost upstream path/configuration binding: '+id);
  demand(!discovered.has(id),'Duplicate discovered configuration identity: '+id);
  demand(test.status.toLowerCase()==='skipped','Unimplemented test must remain skipped; got '+test.status+' for '+id);
  discovered.add(id);
 }
 demand(discovered.size===registered.size,'Missing discovered configuration Facts: '+(registered.size-discovered.size));
 demand(summary.tests===registered.size&&summary.skipped===registered.size&&
  summary.passed===0&&summary.failed===0&&summary.pending===0&&summary.other===0,
  'CTRF summary must report every registered placeholder as skipped, no PASS/FAIL/pending/other');
 const result={registered:registered.size,discovered:discovered.size,skippedUnimplemented:registered.size,
  upstreamSkippedMetadata,passed:0,failed:0,notRun:0,releaseEligible:false};
 if(release)throw Error('Release blocked: '+registered.size+' UNIMPLEMENTED placeholders remain');
 return result;
}

export function readDiscoveryEvidence(directory,ctrfPath,{expected=15523,release=false}={}){
 const ledger=readFileSync(join(resolve(directory),'targets.jsonl'),'utf8');
 demand(ledger.endsWith('\n'),'Truncated source-bound target ledger');
 const targets=ledger.trimEnd().split('\n').map((s,i)=>{try{return JSON.parse(s)}catch{throw Error('Malformed target record at '+i)}});
 const report=JSON.parse(readFileSync(resolve(ctrfPath),'utf8'));
 return reconcileUpstreamDiscovery(targets,report,{expected,release});
}

if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
 try{
  const [directory,ctrf,flag]=process.argv.slice(2);
  demand(directory&&ctrf&&[undefined,'--release'].includes(flag)&&process.argv.length<=5,
   'Usage: node tools/verify-upstream-discovery.ts GENERATED_DIR CTRF_REPORT.json [--release]');
  // Always revalidate the complete generated source/test catalogue before
  // trusting any execution report. This import is CLI-only to keep pure tests fast.
  const {verifyPlaceholderBundle}=await import('./verify-upstream-placeholders.ts');
  verifyPlaceholderBundle(directory);
  console.log(JSON.stringify(readDiscoveryEvidence(directory,ctrf,{release:flag==='--release'})));
 }catch(error){console.error(error.message);process.exitCode=1;}
}
