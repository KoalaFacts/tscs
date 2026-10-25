// All pinned source identities -> raw selected config -> actual JSON parser.
// Pre-VFS syntax profile only. No effective-options/original compiler assertions.
import {readFileSync,writeFileSync,mkdirSync,existsSync,lstatSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {parseTree,classifyAsset,sha256,stableId} from './ts7-scope.ts';
import {buildFixtureHarness,validateReferenceBindings} from './fixture-corpus.ts';
import {reconcileConfigAst,verifyConfigAstArtifacts} from './source-config-ast.ts';
const [up,inv,out]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
if(!up||!inv||!out||process.argv.length!==5||existsSync(out))throw Error('Usage: node tools/compare-source-config-ast.ts PINNED_CHECKOUT INVENTORY NEW_OUTPUT');
const upstream=resolve(up),inventory=resolve(inv),output=resolve(out),dotnet=process.env.DOTNET??'dotnet',go=process.env.GO??'go';
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'},maxBuffer:64*1024*1024});
if(Number(process.versions.node.split('.')[0])!==24||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin||git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length)throw Error('Exact clean reference/Node24 required');
const goVersion=execFileSync(go,['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const summary=JSON.parse(readFileSync(join(inventory,'summary.json'),'utf8')),fixtureBytes=readFileSync(join(inventory,'fixtures.jsonl'));
if(summary.provenance.commit!==pin||summary.artifacts.find(a=>a.path==='fixtures.jsonl')?.sha256!==sha256(fixtureBytes))throw Error('Inventory identity/hash mismatch');
const fixtures=fixtureBytes.toString('utf8').trimEnd().split('\n').map(l=>JSON.parse(l)),byPath=new Map(fixtures.map(f=>[f.path,f]));
const tree=parseTree(git('ls-tree','-r','-z','--full-tree',pin)).filter(e=>classifyAsset(e.path).role==='fixture-input');
if(byPath.size!==fixtures.length||tree.length!==fixtures.length||tree.some(e=>!byPath.has(e.path)))throw Error('Fixture omission/duplicate');
const sources=[],requests=tree.map(e=>{
 const f=byPath.get(e.path),path=join(upstream,e.path);if(e.type!=='blob'||!['100644','100755'].includes(e.mode)||!lstatSync(path).isFile()||classifyAsset(e.path).domain!==f.domain)throw Error('Fixture mode/domain mismatch');
 const bytes=readFileSync(path),oid=createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
 if(oid!==e.oid||sha256(bytes)!==f.rawSha256||f.id!==stableId(stableId(pin,'asset',e.path),'fixture',e.path))throw Error('Source identity/bytes mismatch');
 sources.push({sourceId:f.id,path:f.path,rawSha256:f.rawSha256,decoding:f.decoding,upstreamSelection:f.upstreamSelection});
 return {id:stableId(f.id,'selected-config-ast-profile','default-options/pre-vfs'),input:bytes.toString('base64'),fileName:Buffer.from(e.path).toString('base64'),implicit:false,failAt:0,returnNil:false,profile:'config-ast'};
});
mkdirSync(output,{recursive:true});const save=(p,v)=>writeFileSync(join(output,p),v,{flag:'wx',flush:true});
save('requests.json',JSON.stringify(requests));
const overlay=join(output,'overlay.json');save('overlay.json',JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:resolve('tools/fixture-harness/state_oracle_test.go'),[join(upstream,'tsc/internal/testrunner/tscs_config_ast_selection_oracle_test.go')]:resolve('tools/fixture-harness/config_ast_selection_oracle_test.go'),[join(upstream,'tsc/internal/parser/tscs_source_config_ast_test.go')]:resolve('tools/fixture-harness/source_config_ast_oracle_test.go')}}));
function runGo(name,pkg,environment,log){const r=spawnSync(go,['test','-overlay',overlay,'-run','^'+name+'$','-count=1',pkg],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',...environment}});save(log,(r.stdout??'')+(r.stderr??''));if(r.error||r.signal||r.status!==0)throw Error('Reference execution failed: '+(r.error??r.stderr));}
runGo('TestTscsConfigAstSelectionOracle','./internal/testrunner',{TSCS_STATE_INPUT:join(output,'requests.json'),TSCS_STATE_OUTPUT:join(output,'selection.json')},'selection.log');
const selection=JSON.parse(readFileSync(join(output,'selection.json'),'utf8'));validateReferenceBindings(requests,selection);
const parseRequests=selection.filter(r=>r.expected.panic===null&&r.expected.configFile!==null).map(r=>({id:r.id,sourceBytes:r.expected.configFile.content,fileName:r.context.configPath}));
save('parse-requests.json',JSON.stringify(parseRequests));
runGo('TestTscsSourceConfigAstOracle','./internal/parser',{TSCS_STRING_INPUT:join(output,'parse-requests.json'),TSCS_STRING_OUTPUT:join(output,'reference.json')},'reference.log');
buildFixtureHarness(dotnet,resolve('tools/Tscs.FixtureHarness/Tscs.FixtureHarness.csproj'),join(output,'build.log'));
// Capture each child stdout through a pipe, then persist bounded immutable
// Node-owned shards. Do not inherit a writable artifact FD into the child:
// prior monolithic outputs were observed truncated after process completion.
const actual=[],actualArtifacts=[],calls=[],actualStart=Date.now();let actualLog='';
for(let offset=0;offset<requests.length;offset+=256){
 const batch=requests.slice(offset,offset+256),name='actual-'+String(calls.length).padStart(3,'0')+'.jsonl';
 const remaining=180000-(Date.now()-actualStart);if(remaining<=0)throw Error('C# profile TIMEOUT');
 const run=spawnSync(dotnet,[resolve('tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll')],{input:batch.map(r=>JSON.stringify(r)).join('\n')+'\n',encoding:'utf8',timeout:remaining,maxBuffer:64*1024*1024});
 const stdout=run.stdout??'',stderr=run.stderr??'';save(name,stdout);actualArtifacts.push(name);actualLog+=stderr;
 if(run.error||run.signal||run.status!==0){save('actual.log',actualLog);throw Error('C# profile CRASH/TIMEOUT: '+(run.error??stderr));}
 const rows=stdout.trimEnd().split('\n').filter(Boolean).map(l=>JSON.parse(l));
 if(rows.length!==batch.length||rows.some((r,i)=>r.id!==batch[i].id))throw Error('C# batch binding/count mismatch');
 actual.push(...rows);calls.push({artifact:name,offset,requests:batch.length,status:run.status,stdoutBytes:Buffer.byteLength(stdout),stdoutSha256:sha256(stdout),stderrSha256:sha256(stderr)});
}
save('actual.log',actualLog);save('actual-calls.json',JSON.stringify(calls,null,2)+'\n');
const reference=JSON.parse(readFileSync(join(output,'reference.json'),'utf8'));
const results=reconcileConfigAst(sources,requests,actual,selection,reference);save('results.jsonl',results.map(r=>JSON.stringify(r)).join('\n')+'\n');
const configs=results.filter(r=>r.config);save('configs.jsonl',configs.map(r=>JSON.stringify(r)).join('\n')+'\n');
const counts={sources:requests.length,selectedConfigSources:parseRequests.length,upstreamSkippedSources:sources.filter(r=>r.upstreamSelection.status==='UPSTREAM_SKIPPED').length,nonRawUtf8Sources:fixtures.filter(f=>f.decoding!=='UTF8').length,referenceProjectedNodes:reference.reduce((n,r)=>n+r.nodes.length,0),referenceDiagnostics:reference.reduce((n,r)=>n+r.diagnostics.length,0),referenceErrorSources:reference.filter(r=>r.diagnostics.length).length};
for(const status of ['MATCH','MATCH_DIAGNOSTICS','NOT_APPLICABLE','UNSUPPORTED','MISMATCH','REFERENCE_PANIC','CRASH'])counts[status]=results.filter(r=>r.status===status).length;
const sourceFiles=['tools/compare-source-config-ast.ts','tools/source-config-ast.ts','tools/ts7-scope.ts','tools/fixture-corpus.ts','tools/Tscs.FixtureHarness/Program.cs','tools/Tscs.FixtureHarness/Tscs.FixtureHarness.csproj','tools/fixture-harness/FixtureConfigAst.cs','tools/fixture-harness/FixtureConfigSelection.cs','tools/fixture-harness/FixtureProtocol.cs','tools/fixture-harness/FixtureLoader.cs','tools/fixture-harness/FixtureDirectives.cs','tools/fixture-harness/FixtureTrivia.cs','tools/fixture-harness/FixturePaths.cs','tools/fixture-harness/PinnedGoLowercase.cs','tools/fixture-harness/go-lowercase.json','tools/fixture-harness/state_oracle_test.go','tools/fixture-harness/config_ast_selection_oracle_test.go','tools/fixture-harness/source_config_ast_oracle_test.go','src/Tscs.Compiler/SourceDecoder.cs','src/Tscs.Compiler/JsonSyntax.cs','src/Tscs.Compiler/JsonNumbers.cs','src/Tscs.Compiler/Scanner.cs','src/Tscs.Compiler/Syntax.cs','src/Tscs.Compiler/AssemblyInfo.cs','Directory.Build.props','global.json'];
const referenceFiles=['tsc/internal/testrunner/test_case_parser.go','tsc/internal/testutil/harnessutil/harnessutil.go','tsc/internal/parser/parser.go','tsc/internal/scanner/scanner.go','tsc/internal/stringutil/util.go','tsc/internal/jsnum/string.go','tsc/internal/diagnostics/diagnostics_generated.go','tsc/internal/ast/tokenflags.go','tsc/internal/vfs/osvfs/os.go','tsc/internal/tspath/path.go','tsc/internal/tspath/rooted_path.go'];
const artifactNames=['requests.json','selection.json','selection.log','parse-requests.json','reference.json','reference.log',...actualArtifacts,'actual-calls.json','actual.log','results.jsonl','configs.jsonl','build.log'];
const report={upstreamCommit:pin,profile:'all-sources/default-options/pre-vfs/selected-config-json-ast',counts,goVersion,nodeVersion:process.version,dotnetSdk:execFileSync(dotnet,['--version'],{encoding:'utf8'}).trim(),sourceHashes:Object.fromEntries(sourceFiles.map(p=>[p,sha256(readFileSync(p))])),referenceHashes:Object.fromEntries(referenceFiles.map(p=>[p,sha256(git('show',`${pin}:${p}`))])),assemblies:Object.fromEntries(['Tscs.FixtureHarness.dll','Tscs.Compiler.dll'].map(p=>[p,sha256(readFileSync('tools/Tscs.FixtureHarness/bin/Release/net10.0/'+p))])),fixtureInventorySha256:sha256(fixtureBytes),artifacts:artifactNames.map(path=>({path,sha256:sha256(readFileSync(join(output,path))),bytes:readFileSync(join(output,path)).length})),compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN',concreteCases:null,releaseEligible:false,limitations:['Pre-VFS only: actual raw parser/config-name helper plus mirrored selection/removal; not whole makeUnitsFromTest or effective options','Actual Go ParseSourceFile(ScriptKindJSON) consumes selected content bytes; unknown projection kinds explicitly UNSUPPORTED, complete diagnostics retained','No-config sources are NOT_APPLICABLE, never compiler passes; upstream skip metadata retained','Single default-options syntax profile; configured/runtime variants and full test-domain denominator remain unresolved']};
save('summary.json',JSON.stringify(report,null,2)+'\n');verifyConfigAstArtifacts(output);
console.log(JSON.stringify(counts));if(counts.MATCH+counts.NOT_APPLICABLE!==counts.sources)process.exitCode=1;
