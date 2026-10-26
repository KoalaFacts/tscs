import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';

// This is an immutable historical configuration-target inventory, not a claim
// that the current product implements none of the underlying language syntax.
export const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
export const inputContract={
 archiveSha256:'4a11e9a0452607285bb7e453db0276f656c3bfc47bcd84e4daff5d18efae4835',
 archiveLibraryId:'libfile_bc0f70bc02cc8191bb14c3b962ff0072',
 prefix:'source-config-verified-repeat/',
 results:{bytes:14217317,sha256:'80330365c0f0ef7f123bcffa0ec3fc10c2dc3506b8380955d51dafcfd7b631bb'},
 summary:{bytes:7931,sha256:'507c4603c1040ac70b2788e050028a1e0a982bcea3c9d3cb72c9336598dbb557'},
};
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const object=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
const identity=(x,kind)=>typeof x==='string'&&new RegExp('^'+kind+':[a-f0-9]{64}$').test(x);
const demand=(condition,message)=>{if(!condition)throw Error(message);};
const notRun=x=>demand(['compiler','referenceCompiler','acceptance'].every(k=>x[k]==='NOT_RUN'),'Existing acceptance/execution is not NOT_RUN; never downgrade it to SKIP');
export function checkBytes(bytes,expected){
 demand(bytes.length===expected.bytes,'Artifact length mismatch');
 demand(hash(bytes)===expected.sha256,'Artifact hash mismatch');
}
export function buildCatalogue(rows,upstream){
 demand(upstream===pin,'Wrong upstream pin');
 demand(Array.isArray(rows)&&rows.length>0,'Reject empty source scope');
 const sources=new Set(),paths=new Set(),profiles=new Set(),configIds=new Set(),targets=[],blocked=[];
 let upstreamSkippedSources=0;
 const byRunner={compiler:0,transpile:0},byGroup={};
 for(const row of rows){
  demand(object(row)&&identity(row.sourceId,'fixture')&&identity(row.id,'source-configuration-profile'),'Malformed source identity');
  demand(!sources.has(row.sourceId)&&!paths.has(row.path)&&!profiles.has(row.id),'Duplicate source identity/path');
  sources.add(row.sourceId);paths.add(row.path);profiles.add(row.id);
  const m=typeof row.path==='string'&&/^tsc\/testdata\/tests\/cases\/(compiler|conformance|transpile)\/(.+)\.tsx?$/.exec(row.path);
  demand(m&&!row.path.split('/').some(x=>x==='..'||x==='.'||x==='')&&!/[\\\x00-\x1f]/.test(row.path),'Unsafe/non-fixture upstream path');
  const runner=m[1]==='transpile'?'transpile':'compiler';
  demand(row.profile===runner+'/source-settings/configurations','Source/runner profile disagreement');
  demand(/^[a-f0-9]{64}$/.test(row.rawSha256??''),'Missing source content hash');
  demand(object(row.upstreamSelection)&&['SELECTED_BY_EXTENSION','UPSTREAM_SKIPPED'].includes(row.upstreamSelection.status),'Unknown upstream selection policy');
  const upstreamSkipped=row.upstreamSelection.status==='UPSTREAM_SKIPPED';
  if(upstreamSkipped)upstreamSkippedSources++;
  notRun(row);
  if(row.status!=='MATCH'){
   demand(['REFERENCE_FATAL','REFERENCE_PANIC','MISMATCH','UNSUPPORTED','CRASH'].includes(row.status)&&row.configurations===null,'Invalid blocked source or discarded configurations');
   blocked.push({sourceId:row.sourceId,upstreamPath:row.path,rawSha256:row.rawSha256,profile:row.profile,status:row.status,upstreamSelection:row.upstreamSelection,actualOutcome:row.actualOutcome,referenceOutcome:row.referenceOutcome,concreteCases:null});
   continue;
  }
  demand(row.equal===true&&row.actualOutcome==='RESULT'&&row.referenceOutcome==='RESULT','MATCH must be an equal configuration result');
  demand(Array.isArray(row.configurations)&&row.configurations.length>0,'Missing/empty configurations');
  for(const [index,entry] of row.configurations.entries()){
   demand(object(entry)&&identity(entry.id,'configuration')&&entry.index===index,'Malformed configuration identity/index');
   demand(!configIds.has(entry.id),'Duplicate configuration identity');configIds.add(entry.id);notRun(entry);
   const c=entry.configuration;
   demand(c===null||(object(c)&&Object.keys(c).sort().join(',')==='Config,Name'&&typeof c.Name==='string'&&object(c.Config)&&Object.values(c.Config).every(x=>typeof x==='string')),'Malformed named/fallback configuration');
   const relative=row.path.slice('tsc/testdata/tests/cases/'.length),pieces=relative.split('/');
   const group=pieces.length>2?pieces.slice(0,2).join('/'):pieces[0];
   const missingCapability=runner==='transpile'?'ORIGINAL_TRANSPILE_RUNNER':'ORIGINAL_COMPILER_RUNNER';
   targets.push({id:entry.id,sourceId:row.sourceId,upstreamCommit:pin,upstreamPath:row.path,rawSha256:row.rawSha256,sourceProfileId:row.id,profile:row.profile,configuration:c,configurationIndex:index,group,upstreamSelection:row.upstreamSelection,implementation:'UNIMPLEMENTED',implementationScope:'ORIGINAL_RUNNER_END_TO_END_ASSERTIONS',productFeatureImplementation:'UNASSESSED',missingCapability,status:'SKIP',reason:'UNIMPLEMENTED',compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'});
   byRunner[runner]++;byGroup[group]=(byGroup[group]??0)+1;
  }
 }
 return {targets,blocked,summary:{schemaVersion:1,upstreamCommit:pin,kind:'SOURCE_CONFIGURATION_TARGET_PLACEHOLDERS',sources:rows.length,configurationTargets:targets.length,skippedUnimplemented:targets.length,implementedEndToEndTargets:0,accepted:0,blockedSources:blocked.length,upstreamSkippedSources,upstreamSkippedTargetMetadata:targets.filter(t=>t.upstreamSelection.status==='UPSTREAM_SKIPPED').length,byRunner,byGroup,completeRuntimeCases:null,scopeComplete:false,releaseEligible:false,additionalScope:['Go/JS dynamic cases and execution profiles','Fourslash/API/build/watch','Effective options, original runner assertions and output variants'],historicalInput:true}};
}
const csLiteral=value=>JSON.stringify(value).replace(/[\u007f-\uffff]/g,c=>'\\u'+c.charCodeAt(0).toString(16).padStart(4,'0'));
export function renderCSharp(targets,className='UpstreamConfigurationPlaceholders'){
 demand(/^[A-Za-z_][A-Za-z0-9_]*$/.test(className),'Invalid generated class name');
 let code='// Generated configuration TARGETS, not full runtime test expansion.\nusing System;\nusing Xunit;\nnamespace Tscs.Upstream.Placeholders;\n\n[Trait("Scope", "UpstreamConfigurationPlaceholder")]\npublic class '+className+'\n{\n';
 for(const target of targets){
  demand(identity(target.id,'configuration')&&target.status==='SKIP'&&target.reason==='UNIMPLEMENTED','Only explicitly unimplemented targets may be rendered as SKIP');
  const name=target.upstreamPath+' ['+(target.configuration?.Name||'default')+'] '+target.id;
  code+='    [Fact(DisplayName = '+csLiteral(name)+', Skip = '+csLiteral('UNIMPLEMENTED: '+target.missingCapability)+')]\n';
  code+='    public void Target_'+target.id.slice('configuration:'.length)+'() =>\n        throw new NotImplementedException('+csLiteral(target.id)+');\n\n';
 }
 return code+'}\n';
}
export function generate(resultsPath,summaryPath,output){
 const raw=readFileSync(resultsPath),rawSummary=readFileSync(summaryPath);
 checkBytes(raw,inputContract.results);checkBytes(rawSummary,inputContract.summary);
 const sourceSummary=JSON.parse(rawSummary.toString('utf8'));
 const cat=buildCatalogue(raw.toString('utf8').trimEnd().split('\n').map(l=>JSON.parse(l)),sourceSummary.upstreamCommit);
 demand(cat.summary.sources===13006&&cat.summary.configurationTargets===15523&&cat.summary.blockedSources===7&&cat.summary.upstreamSkippedSources===42,'Pinned source/configuration reconciliation failed');
 demand(sourceSummary.counts.runnerConfigurations===cat.summary.configurationTargets&&sourceSummary.counts.sources===cat.summary.sources,'Historical summary disagrees with ledger');
 demand(!existsSync(output),'Output already exists; preserve old evidence');mkdirSync(output,{recursive:true});
 const artifacts=[];
 const save=(name,value)=>{const bytes=Buffer.from(value);writeFileSync(join(output,name),bytes,{flag:'wx'});artifacts.push({path:name,bytes:bytes.length,sha256:hash(bytes)});};
 save('targets.jsonl',cat.targets.map(t=>JSON.stringify(t)).join('\n')+'\n');
 save('blocked-sources.json',JSON.stringify(cat.blocked,null,2)+'\n');
 for(let start=0;start<cat.targets.length;start+=250){const name='ConfigurationTargets_'+String(start/250+1).padStart(3,'0');save(name+'.cs',renderCSharp(cat.targets.slice(start,start+250),name));}
 save('Tscs.Upstream.Placeholders.csproj','<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n    <OutputType>Exe</OutputType>\n    <IsTestProject>true</IsTestProject>\n    <IsPackable>false</IsPackable>\n    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>\n  </PropertyGroup>\n  <ItemGroup><PackageReference Include="xunit.v3.mtp-v2" Version="4.0.2" /></ItemGroup>\n</Project>\n');
 const report={...cat.summary,inputContract,artifacts,dotnetDiscovery:'NOT_RUN',compilerExecution:'NOT_RUN',sourceGeneration:'GENERATED_NOT_COMPILED'};
 save('README.md','# Upstream configuration-target placeholders\n\n15,523 named/fallback configuration targets from 13,006 source fixtures; seven reference-fatal sources remain in blocked-sources.json. Each generated xUnit Fact is explicitly SKIP (UNIMPLEMENTED: ORIGINAL_COMPILER_RUNNER or ORIGINAL_TRANSPILE_RUNNER). Removing Skip without implementing the body throws, never passes.\n\nThis is a backlog, not 15,523 accepted or fully expanded upstream runtime cases. All existing local product tests and guards remain separate. Preserve the 42 upstream-skipped-source flags (39 generated target flags); they are not automatic waivers.\n\nThe supplied project matches the existing xUnit4/MTP2 package pin. Its compile/discovery was NOT_RUN when generated. Do not add it to the strict internal regression solution unchanged: validate a separate placeholder lane and reconcile every discovered SKIP first. Original assertions and missing runtime profiles must be implemented before acceptance; do not just remove a skip or assert true.\n');
 writeFileSync(join(output,'summary.json'),JSON.stringify(report,null,2)+'\n',{flag:'wx'});
 return report;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
 try{
  const args=process.argv.slice(2);demand(args.length===3,'Usage: node tools/upstream-placeholders.ts RESULTS_JSONL SUMMARY_JSON NEW_OUTPUT');
  const result=generate(...args);console.log(JSON.stringify(result,null,2));
 }catch(error){console.error(error.message);process.exitCode=1;}
}
