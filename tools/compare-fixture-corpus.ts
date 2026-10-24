// Full source-fixture raw loading differential; never compiler acceptance.
import {readFileSync,writeFileSync,mkdirSync,existsSync,openSync,closeSync,lstatSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {parseTree,classifyAsset,sha256,stableId} from './ts7-scope.ts';
import {reconcileResults,buildFixtureHarness,validateReferenceBindings} from './fixture-corpus.ts';

const [upstreamArg,inventoryArg,outputArg,profileArg]=process.argv.slice(2);
if(!upstreamArg||!inventoryArg||!outputArg||process.argv.length>6||profileArg!==undefined&&profileArg!=='file-map')
 throw Error('Usage: node tools/compare-fixture-corpus.ts CLEAN_PINNED_CHECKOUT INVENTORY NEW_OUTPUT [file-map]');
const mapProfile=profileArg==='file-map';
const upstream=resolve(upstreamArg),inventory=resolve(inventoryArg),output=resolve(outputArg);
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8')).commit;
if(pin!=='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f')throw Error('Reference pin needs separate review');
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'},maxBuffer:64*1024*1024});
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length)throw Error('Exact clean checkout required');
const go=process.env.GO??'go',dotnet=process.env.DOTNET??'dotnet';
const goVersion=execFileSync(go,['version'],{encoding:'utf8'}).trim();
if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
if(Number(process.versions.node.split('.')[0])!==24)throw Error('Node 24 required');
if(existsSync(output))throw Error('Output already exists; preserve prior evidence');
const jsonl=path=>readFileSync(path,'utf8').trimEnd().split('\n').filter(Boolean).map(s=>JSON.parse(s));
const summary=JSON.parse(readFileSync(join(inventory,'summary.json'),'utf8'));
const fixtureBytes=readFileSync(join(inventory,'fixtures.jsonl'));
if(summary.provenance.commit!==pin||summary.artifacts.find(a=>a.path==='fixtures.jsonl')?.sha256!==sha256(fixtureBytes))throw Error('Inventory identity/hash mismatch');
const fixtures=jsonl(join(inventory,'fixtures.jsonl'));
const tree=parseTree(git('ls-tree','-r','-z','--full-tree',pin)).filter(e=>classifyAsset(e.path).role==='fixture-input');
const byPath=new Map(fixtures.map(f=>[f.path,f]));
if(byPath.size!==fixtures.length||tree.length!==fixtures.length||tree.some(e=>!byPath.has(e.path)))throw Error('Fixture tree/inventory omissions or duplicates');
const requests=tree.map(e=>{
 const f=byPath.get(e.path),path=join(upstream,e.path);
 if(e.type!=='blob'||!['100644','100755'].includes(e.mode)||!lstatSync(path).isFile())throw Error('Unexpected fixture asset mode');
 const bytes=readFileSync(path),oid=createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
 if(oid!==e.oid||sha256(bytes)!==f.rawSha256||f.id!==stableId(stableId(pin,'asset',e.path),'fixture',e.path))throw Error('Source bytes/identity mismatch: '+e.path);
 return {id:f.id,input:bytes.toString('base64'),fileName:Buffer.from(e.path).toString('base64'),implicit:false,failAt:0,returnNil:false,...(mapProfile?{profile:'file-map'}:{})};
});
mkdirSync(output,{recursive:true});
const save=(name,bytes)=>writeFileSync(join(output,name),bytes,{flag:'wx'});
buildFixtureHarness(dotnet,resolve('tools/Tscs.FixtureHarness/Tscs.FixtureHarness.csproj'),join(output,'build.log'));
save('requests.json',JSON.stringify(requests));
const probe=resolve(mapProfile?'tools/fixture-harness/map_oracle_test.go':'tools/fixture-harness/state_oracle_test.go'),overlay=join(output,'overlay.json');
save('overlay.json',JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:resolve('tools/fixture-harness/state_oracle_test.go'),...(mapProfile?{[join(upstream,'tsc/internal/testrunner/tscs_map_oracle_test.go')]:probe}:{})}}));
const refPath=join(output,'reference.json');
const ref=spawnSync(go,['test','-overlay',overlay,'-run',mapProfile?'^TestTscsFileMapOracle$':'^TestTscsFixtureStateOracle$','-count=1','./internal/testrunner'],
 {cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_STATE_INPUT:join(output,'requests.json'),TSCS_STATE_OUTPUT:refPath}});
save('reference.log',(ref.stdout??'')+(ref.stderr??''));
if(ref.error||ref.status!==0||ref.signal)throw Error('Go oracle failed: '+(ref.error??ref.stderr));
const dll=resolve('tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll');
const actualPath=join(output,'actual.jsonl'),fd=openSync(actualPath,'wx');let actual;
try{actual=spawnSync(dotnet,[dll],{input:requests.map(x=>JSON.stringify(x)).join('\n')+'\n',stdio:['pipe',fd,'pipe'],encoding:'utf8',timeout:180000});}
finally{closeSync(fd);}
save('actual.log',actual.stderr??'');
if(actual.error||actual.status!==0||actual.signal)throw Error('C# runner failed: '+(actual.error??actual.stderr));
const reference=JSON.parse(readFileSync(refPath,'utf8'));validateReferenceBindings(requests,reference);
const results=reconcileResults(fixtures.map(f=>({id:f.id,path:f.path,rawSha256:f.rawSha256,upstreamSelection:f.upstreamSelection})),jsonl(actualPath),reference,mapProfile?'file-map':'raw');
save('results.jsonl',results.map(r=>JSON.stringify(r)).join('\n')+'\n');
const counts={sources:results.length,rawUnits:0,callbacks:0,upstreamSkippedSources:results.filter(r=>r.upstreamSelection.status==='UPSTREAM_SKIPPED').length};
for(const row of jsonl(actualPath)){counts.rawUnits+=mapProfile?row.expected.unitCount??0:row.expected.units?.length??0;counts.callbacks+=mapProfile?row.expected.unitCount??0:row.expected.calls??0;}
for(const status of ['MATCH','MISMATCH','REFERENCE_PANIC','REFERENCE_ERROR','CRASH'])counts[status]=results.filter(r=>r.loading===status).length;
if(mapProfile){const rows=jsonl(actualPath);counts.fileEntries=rows.reduce((n,r)=>n+(r.expected.files?.length??0),0);counts.links=rows.reduce((n,r)=>n+(r.expected.links?.length??0),0);}
const artifactNames=['requests.json','reference.json','actual.jsonl','results.jsonl','build.log','reference.log','actual.log'];
const sourceFiles=['tools/compare-fixture-corpus.ts','tools/fixture-corpus.ts','tools/fixture-harness/FixtureFileMap.cs','tools/fixture-harness/FixturePaths.cs','tools/fixture-harness/map_oracle_test.go','tools/fixture-harness/state_oracle_test.go','tools/fixture-harness/FixtureProtocol.cs','tools/fixture-harness/FixtureLoader.cs','tools/fixture-harness/FixtureTrivia.cs','tools/fixture-harness/FixtureDirectives.cs','src/Tscs.Compiler/SourceDecoder.cs','tools/Tscs.FixtureHarness/Program.cs','tools/Tscs.FixtureHarness/Tscs.FixtureHarness.csproj','Directory.Build.props','global.json'];
const sourcePaths=['tsc/internal/testrunner/compiler_runner.go','tsc/internal/tsoptions/tsoptionstest/vfs.go','tsc/internal/testrunner/test_case_parser.go','tsc/internal/scanner/scanner.go','tsc/internal/tspath/path.go','tsc/internal/vfs/internal/internal.go'];
const report={upstreamCommit:pin,profile:mapProfile?'file-map/default-options/rooted-files-and-link-projections':'raw/default-options/unit-snapshot-callback',scope:'all selected compiler/conformance/transpile source fixtures including upstream skips',
 counts,goVersion,nodeVersion:process.version,dotnetSdk:execFileSync(dotnet,['--version'],{encoding:'utf8'}).trim(),
 sourceHashes:Object.fromEntries(sourceFiles.map(p=>[p,sha256(readFileSync(p))])),referenceHashes:Object.fromEntries(sourcePaths.map(p=>[p,sha256(git('show',`${pin}:${p}`))])),
 assemblies:Object.fromEntries([dll,resolve('tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.Compiler.dll')].map(p=>[p.slice(p.lastIndexOf('/')+1),sha256(readFileSync(p))])),
 inventorySummarySha256:sha256(readFileSync(join(inventory,'summary.json'))),fixtureInventorySha256:sha256(fixtureBytes),
 artifacts:artifactNames.map(path=>({path,bytes:readFileSync(join(output,path)).length,sha256:sha256(readFileSync(join(output,path)))})),
 compiler:'NOT_RUN',referenceCompiler:'NOT_RUN',acceptance:'NOT_RUN',releaseEligible:false,
 limitations:['No AST/config/VFS construction; file-map profile includes only pre-VFS paths and files, preserving colliding normalized aliases separately','Implicit-first and callback-error profiles have separate 151 synthetic/boundary vectors, not full-corpus executions','Not all test domains or configuration/runtime variants are expanded']};
save('summary.json',JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(counts));
if(counts.MATCH!==counts.sources)process.exitCode=1;
