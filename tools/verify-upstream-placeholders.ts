// Independent post-generation integrity check. Deliberately does not call the
// catalogue producer or treat xUnit discovery/SKIP as compiler acceptance.
import {readFileSync,readdirSync,lstatSync} from 'node:fs';
import {resolve,join,basename} from 'node:path';
import {createHash} from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {stableId} from './ts7-scope.ts';
import {canonicalJson} from './fixture-corpus.ts';

const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
const hash=b=>createHash('sha256').update(b).digest('hex');
const demand=(ok,message)=>{if(!ok)throw Error(message);};
const id=(s,prefix)=>typeof s==='string'&&new RegExp('^'+prefix+':[a-f0-9]{64}$').test(s);
const object=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
const eq=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
const readJson=(directory,name)=>JSON.parse(readFileSync(join(directory,name),'utf8'));
const csharpFact=/\[Fact\(DisplayName = ("(?:\\.|[^"\\])*"), Skip = ("(?:\\.|[^"\\])*")\)\]\s*public void Target_([a-f0-9]{64})\(\) =>\s*throw new NotImplementedException\(("(?:\\.|[^"\\])*")\);/g;

export function verifyPlaceholderBundle(directory,{expectedTargets=15523,expectedSources=13006,release=false}={}){
 directory=resolve(directory);
 const summary=readJson(directory,'summary.json');
 demand(summary.schemaVersion===1&&summary.kind==='SOURCE_CONFIGURATION_TARGET_PLACEHOLDERS'&&summary.upstreamCommit===pin,'Incorrect placeholder schema or upstream pin');
 demand(Number.isSafeInteger(expectedTargets)&&expectedTargets>0&&Number.isSafeInteger(expectedSources)&&expectedSources>0,'Explicit nonzero target/source denominator required');
 demand(summary.configurationTargets===expectedTargets&&summary.sources===expectedSources,'Source/target inventory count drift');
 demand(summary.skippedUnimplemented===expectedTargets&&summary.accepted===0&&summary.implementedEndToEndTargets===0,'Unexpected PASS or incomplete skip inventory');
 demand(summary.releaseEligible===false&&summary.scopeComplete===false&&summary.completeRuntimeCases===null,'Source-configuration placeholders cannot grant release acceptance');
 demand(Array.isArray(summary.artifacts),'Missing generation artifacts manifest');
 const shardCount=Math.ceil(expectedTargets/250);
 const shardNames=Array.from({length:shardCount},(_,i)=>`ConfigurationTargets_${String(i+1).padStart(3,'0')}.cs`);
 const paths=['targets.jsonl','blocked-sources.json',...shardNames,'Tscs.Upstream.Placeholders.csproj','README.md'];
 demand(eq(summary.artifacts.map(a=>a.path),paths),'Missing, extra or reordered required generation artifacts');
 for(const a of summary.artifacts){
  demand(typeof a.path==='string'&&basename(a.path)===a.path&&/^[A-Za-z0-9_.-]+$/.test(a.path),'Unsafe artifact path');
  const file=join(directory,a.path);
  demand(lstatSync(file).isFile(),'Required artifact is not a regular file: '+a.path);
  const bytes=readFileSync(file);
  demand(bytes.length===a.bytes&&hash(bytes)===a.sha256,'Required artifact SHA/length mismatch: '+a.path);
 }
 const onDisk=readdirSync(directory).filter(x=>x.endsWith('.cs'));
 demand(eq(onDisk.sort(),shardNames.slice().sort()),'Generated C# shard count/filenames changed');
 const project=readFileSync(join(directory,'Tscs.Upstream.Placeholders.csproj'),'utf8');
 demand(/<TargetFramework>net10\.0<\/TargetFramework>/.test(project)&&/<UseMicrosoftTestingPlatformRunner>true<\/UseMicrosoftTestingPlatformRunner>/.test(project)&&/<PackageReference Include="xunit\.v3\.mtp-v2" Version="4\.0\.2"\s*\/>/.test(project),'Test project no longer uses pinned xUnit4/MTP2');
 const lines=readFileSync(join(directory,'targets.jsonl'),'utf8').trimEnd().split('\n');
 demand(lines.length===expectedTargets,'Configuration target ledger length differs from pinned count');
 const targets=lines.map((line,i)=>{try{return JSON.parse(line)}catch{throw Error('Malformed target JSON at '+i)}});
 const blocked=readJson(directory,'blocked-sources.json');
 demand(Array.isArray(blocked)&&blocked.length===summary.blockedSources,'Blocked-source count mismatch');
 const targetIds=new Set(),sourceIds=new Map(),blockedIds=new Set(),skipSources=new Set();
 const runners={compiler:0,transpile:0},groups={};
 for(const t of targets){
  demand(id(t.id,'configuration')&&!targetIds.has(t.id),'Duplicate or invalid target ID');targetIds.add(t.id);
  demand(id(t.sourceId,'fixture')&&id(t.sourceProfileId,'source-configuration-profile'),'Malformed source binding');
  demand(t.upstreamCommit===pin&&typeof t.rawSha256==='string'&&/^[a-f0-9]{64}$/.test(t.rawSha256),'Pinned content identity mismatch');
  demand(typeof t.upstreamPath==='string'&&/^tsc\/testdata\/tests\/cases\/(compiler|conformance|transpile)\/.+\.tsx?$/.test(t.upstreamPath),'Invalid upstream target path');
  const runner=t.upstreamPath.split('/')[4]==='transpile'?'transpile':'compiler';
  demand(t.profile===runner+'/source-settings/configurations'&&t.missingCapability===(runner==='compiler'?'ORIGINAL_COMPILER_RUNNER':'ORIGINAL_TRANSPILE_RUNNER'),'Profile/runner capability mismatch');
  demand(t.status==='SKIP'&&t.reason==='UNIMPLEMENTED'&&t.implementation==='UNIMPLEMENTED'&&t.implementationScope==='ORIGINAL_RUNNER_END_TO_END_ASSERTIONS'&&t.productFeatureImplementation==='UNASSESSED','Only explicit unimplemented original runner may be skipped');
  demand(t.compiler==='NOT_RUN'&&t.referenceCompiler==='NOT_RUN'&&t.acceptance==='NOT_RUN','Real compiler executions cannot be downgraded to SKIP');
  demand(Number.isInteger(t.configurationIndex)&&t.configurationIndex>=0&&t.configuration!==undefined,'Lost configuration identity');
  const c=t.configuration;demand(c===null||(object(c)&&typeof c.Name==='string'&&object(c.Config)&&Object.values(c.Config).every(x=>typeof x==='string')),'Malformed source configuration');
  demand(t.id===stableId(t.sourceProfileId,'configuration',canonicalJson(c)),'Configuration ID does not match source profile and configuration');
  demand(object(t.upstreamSelection)&&['SELECTED_BY_EXTENSION','UPSTREAM_SKIPPED'].includes(t.upstreamSelection.status),'Missing upstream skip provenance');
  if(t.upstreamSelection.status==='UPSTREAM_SKIPPED')skipSources.add(t.sourceId);
  const previous=sourceIds.get(t.sourceId);
  if(previous)demand(previous.path===t.upstreamPath&&previous.profile===t.sourceProfileId&&previous.raw===t.rawSha256&&previous.next===t.configurationIndex,'Unstable source/configuration identity ordering');
  sourceIds.set(t.sourceId,{path:t.upstreamPath,profile:t.sourceProfileId,raw:t.rawSha256,next:t.configurationIndex+1});
  runners[runner]++;groups[t.group]=(groups[t.group]??0)+1;
 }
 for(const b of blocked){
  demand(id(b.sourceId,'fixture')&&!sourceIds.has(b.sourceId)&&!blockedIds.has(b.sourceId),'Duplicate/overlapping blocked source');blockedIds.add(b.sourceId);
  demand(b.status==='REFERENCE_FATAL'||b.status==='REFERENCE_PANIC'||b.status==='UNSUPPORTED'||b.status==='CRASH'||b.status==='MISMATCH','Invalid blocked source outcome');
  demand(b.concreteCases===null&&typeof b.upstreamPath==='string'&&/^[a-f0-9]{64}$/.test(b.rawSha256),'Blocked source missing provenance');
  demand(object(b.upstreamSelection)&&['SELECTED_BY_EXTENSION','UPSTREAM_SKIPPED'].includes(b.upstreamSelection.status),'Invalid blocked skip metadata');
  if(b.upstreamSelection.status==='UPSTREAM_SKIPPED')skipSources.add(b.sourceId);
 }
 demand(sourceIds.size+blockedIds.size===expectedSources,'Unaccounted source identities');
 demand(eq(runners,summary.byRunner)&&eq(groups,summary.byGroup),'Runner or capability-group summary mismatch');
 demand(skipSources.size===summary.upstreamSkippedSources,'Upstream-skipped sources lost');
 const flagged=targets.filter(t=>t.upstreamSelection.status==='UPSTREAM_SKIPPED').length;
 demand(flagged===summary.upstreamSkippedTargetMetadata,'Upstream skip target count mismatch');
 let cursor=0;
 for(const [i,name] of shardNames.entries()){
  const code=readFileSync(join(directory,name),'utf8');
  demand(code.includes('namespace Tscs.Upstream.Placeholders;')&&code.includes('public class '+name.slice(0,-3)),'Generated shard namespace/class mismatch');
  const matches=[...code.matchAll(csharpFact)];
  const expectedSize=Math.min(250,targets.length-cursor);
  demand(matches.length===expectedSize&&((code.match(/\[Fact\(/g)||[]).length===matches.length)&&((code.match(/public void Target_/g)||[]).length===matches.length),'Missing or additional xUnit Fact/throw body: '+name);
  for(const match of matches){
   const t=targets[cursor++];const display=JSON.parse(match[1]),skip=JSON.parse(match[2]),thrown=JSON.parse(match[4]);
   const title=t.upstreamPath+' ['+(t.configuration?.Name||'default')+'] '+t.id;
   demand(display===title&&match[3]===t.id.slice('configuration:'.length)&&thrown===t.id,'C# Fact does not bind the source/target ID: '+name);
   demand(skip==='UNIMPLEMENTED: '+t.missingCapability,'C# Fact did not explicitly skip missing runner: '+name);
  }
 }
 demand(cursor===expectedTargets,'Undiscovered generated targets');
 const counts={sources:expectedSources,targets:expectedTargets,skippedUnimplemented:expectedTargets,blockedSources:blocked.length,upstreamSkippedSources:skipSources.size,upstreamSkippedTargetMetadata:flagged};
 if(release)throw Error('Release blocked: '+expectedTargets+' registered UNIMPLEMENTED source-configuration targets; full upstream expansion is unknown');
 return counts;
}

if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
 try{
  const [dir,flag]=process.argv.slice(2);
  demand(dir&&[undefined,'--release'].includes(flag)&&process.argv.length<=4,'Usage: node tools/verify-upstream-placeholders.ts GENERATED_DIR [--release]');
  console.log(JSON.stringify(verifyPlaceholderBundle(dir,{release:flag==='--release'})));
 }catch(e){console.error(e.message);process.exitCode=1;}
}
