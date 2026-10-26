import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,rmSync,writeFileSync,readFileSync,unlinkSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {verifyPlaceholderBundle} from './verify-upstream-placeholders.ts';
const hash = b => createHash('sha256').update(b).digest('hex');
const pin = '8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
const id = x => 'configuration:' + x.repeat(64);
const make = () => {
 const dir=mkdtempSync(join(tmpdir(),'tscs-placeholder-'));
 const t={id:id('a'),sourceId:'fixture:'+('b'.repeat(64)),upstreamCommit:pin,upstreamPath:'tsc/testdata/tests/cases/compiler/a.ts',rawSha256:'c'.repeat(64),sourceProfileId:'source-configuration-profile:'+('d'.repeat(64)),profile:'compiler/source-settings/configurations',configuration:{Name:'',Config:{target:'es2015'}},configurationIndex:0,group:'compiler',upstreamSelection:{status:'UPSTREAM_SKIPPED'},implementation:'UNIMPLEMENTED',implementationScope:'ORIGINAL_RUNNER_END_TO_END_ASSERTIONS',productFeatureImplementation:'UNASSESSED',missingCapability:'ORIGINAL_COMPILER_RUNNER',status:'SKIP',reason:'UNIMPLEMENTED',compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN'};
 const b={sourceId:'fixture:'+('e'.repeat(64)),upstreamPath:'tsc/testdata/tests/cases/compiler/moduleNone.ts',rawSha256:'f'.repeat(64),profile:t.profile,status:'REFERENCE_FATAL',upstreamSelection:{status:'UPSTREAM_SKIPPED'},actualOutcome:'FATAL',referenceOutcome:'FATAL',concreteCases:null};
 const display=t.upstreamPath+' [default] '+t.id;
 const cs='// Generated configuration TARGETS, not full runtime test expansion.\nusing System;\nusing Xunit;\nnamespace Tscs.Upstream.Placeholders;\n\n[Trait("Scope", "UpstreamConfigurationPlaceholder")]\npublic class ConfigurationTargets_001\n{\n    [Fact(DisplayName = '+JSON.stringify(display)+', Skip = "UNIMPLEMENTED: ORIGINAL_COMPILER_RUNNER")]\n    public void Target_'+t.id.slice('configuration:'.length)+'() =>\n        throw new NotImplementedException('+JSON.stringify(t.id)+');\n\n}\n';
 const files={'targets.jsonl':JSON.stringify(t)+'\n','blocked-sources.json':JSON.stringify([b])+'\n','ConfigurationTargets_001.cs':cs,'Tscs.Upstream.Placeholders.csproj':'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner></PropertyGroup><ItemGroup><PackageReference Include="xunit.v3.mtp-v2" Version="4.0.2" /></ItemGroup></Project>','README.md':'Only placeholders.\n'};
 const artifacts=Object.entries(files).map(([path,content])=>({path,bytes:Buffer.byteLength(content),sha256:hash(content)}));
 const summary={schemaVersion:1,upstreamCommit:pin,kind:'SOURCE_CONFIGURATION_TARGET_PLACEHOLDERS',sources:2,configurationTargets:1,skippedUnimplemented:1,implementedEndToEndTargets:0,accepted:0,blockedSources:1,upstreamSkippedSources:2,upstreamSkippedTargetMetadata:1,byRunner:{compiler:1,transpile:0},byGroup:{compiler:1},completeRuntimeCases:null,scopeComplete:false,releaseEligible:false,artifacts};
 for(const [path,content] of Object.entries(files))writeFileSync(join(dir,path),content);
 writeFileSync(join(dir,'summary.json'),JSON.stringify(summary)+'\n');
 return {dir,summary,t};
};
const withBundle=(f)=>{const b=make();try{f(b);}finally{rmSync(b.dir,{recursive:true,force:true});}};
const rehash=(dir,path)=>{const s=JSON.parse(readFileSync(join(dir,'summary.json'),'utf8'));const b=readFileSync(join(dir,path));const e=s.artifacts.find(x=>x.path===path);e.sha256=hash(b);e.bytes=b.length;writeFileSync(join(dir,'summary.json'),JSON.stringify(s)+'\n');};
test('complete source-bound SKIP catalogue reconciles every generated C# Fact and blocked source',()=>withBundle(({dir})=>{
 const result=verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2});
 assert.deepEqual(result,{sources:2,targets:1,skippedUnimplemented:1,blockedSources:1,upstreamSkippedSources:2,upstreamSkippedTargetMetadata:1});
}));
test('rejects even rehashed facts that silently drop their throwing body',()=>withBundle(({dir})=>{
 const p=join(dir,'ConfigurationTargets_001.cs');writeFileSync(p,readFileSync(p,'utf8').replace('throw new NotImplementedException','return /* missing assertion */; // throw new NotImplementedException'));rehash(dir,'ConfigurationTargets_001.cs');
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2}),/Fact|body|method|target/i);
}));
test('rejects SKIP status upgraded to PASS without original reference execution',()=>withBundle(({dir})=>{
 const p=join(dir,'targets.jsonl');writeFileSync(p,readFileSync(p,'utf8').replace('"status":"SKIP"','"status":"PASS"'));rehash(dir,'targets.jsonl');
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2}),/skip|status|unimplemented/i);
}));
test('fails when any required artifact is removed together with its manifest entry',()=>withBundle(({dir})=>{
 unlinkSync(join(dir,'ConfigurationTargets_001.cs'));
 const s=JSON.parse(readFileSync(join(dir,'summary.json'),'utf8'));s.artifacts=s.artifacts.filter(x=>x.path!=='ConfigurationTargets_001.cs');writeFileSync(join(dir,'summary.json'),JSON.stringify(s)+'\n');
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2}),/missing|artifact|count/i);
}));
test('rejects changed source/configuration identity despite updated hash and metadata',()=>withBundle(({dir})=>{
 const p=join(dir,'targets.jsonl');writeFileSync(p,readFileSync(p,'utf8').replace('configuration:'+('a'.repeat(64)),'configuration:'+('f'.repeat(64))));rehash(dir,'targets.jsonl');
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2}),/identity|id|C#|Fact|display/i);
}));
test('rejects truncated inventory even if an attacker rewrites its own summary counts',()=>withBundle(({dir})=>{
 writeFileSync(join(dir,'targets.jsonl'),'');rehash(dir,'targets.jsonl');
 const s=JSON.parse(readFileSync(join(dir,'summary.json'),'utf8'));s.configurationTargets=0;s.skippedUnimplemented=0;writeFileSync(join(dir,'summary.json'),JSON.stringify(s)+'\n');
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2}),/target|empty|scope|count/i);
}));
test('release mode rejects all unimplemented placeholders despite clean discovery',()=>withBundle(({dir})=>{
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2,release:true}),/release|unimplemented|skip/i);
}));
test('rejects wrong pinned upstream version or fabricated blocked source',()=>withBundle(({dir})=>{
 const p=join(dir,'blocked-sources.json');writeFileSync(p,'[]\n');rehash(dir,'blocked-sources.json');
 assert.throws(()=>verifyPlaceholderBundle(dir,{expectedTargets:1,expectedSources:2}),/blocked|source/i);
}));
