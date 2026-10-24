import {execFileSync,spawn,spawnSync} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join,dirname,basename} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
import {parseTree,classifyAsset,inspectFixture,initialProgress,stableId,sha256,summarize,validateProgress} from './ts7-scope.mjs';

const directory=dirname(fileURLToPath(import.meta.url));
const repository=resolve(directory,'..');
const decoder=new TextDecoder('utf-8',{fatal:true,ignoreBOM:true});
const policies=[
  ['compiler-selection','tsc/internal/testrunner/compiler_runner.go'],
  ['compiler-variations','tsc/internal/testrunner/options_generated.go'],
  ['fixture-parser','tsc/internal/testrunner/test_case_parser.go'],
  ['configuration-and-option-skips','tsc/internal/testutil/harnessutil/harnessutil.go'],
  ['transpile-selection','tsc/internal/testrunner/transpile_runner.go'],
  ['fourslash-driver','tsc/internal/fourslash/fourslash.go'],
  ['fourslash-parser','tsc/internal/fourslash/test_parser.go'],
  ['project-build-watch-scenarios','tsc/internal/execute/tsctests/runner.go'],
  ['byte-decoding','tsc/internal/vfs/internal/internal.go'],
  ['api-test-generation','packages/typescript/scripts/generateSync.ts'],
  ['task-aliases','Herebyfile.mjs'],
  ['hosted-execution-profiles','.github/workflows/ci.yml'],
];
const command=(upstream,args)=>execFileSync('git',['-C',upstream,...args],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'},maxBuffer:64*1024*1024,stdio:['pipe','pipe','pipe']});

async function readBlobs(upstream,entries,consume) {
  const child=spawn('git',['-C',upstream,'cat-file','--batch'],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
  let stderr='',failure=null;
  child.on('error',e=>{failure=e;}); child.stdin.on('error',e=>{failure=e;});
  child.stderr.on('data',chunk=>{stderr+=chunk.toString();});
  const closed=new Promise(r=>child.on('close',(code,signal)=>r({code,signal})));
  child.stdin.end(entries.map(x=>x.oid).join('\n')+'\n');
  let buffer=Buffer.alloc(0),header=null,index=0;
  try {
    for await(const chunk of child.stdout) {
      buffer=buffer.length?Buffer.concat([buffer,chunk]):chunk;
      while(true) {
        if(!header) {
          const end=buffer.indexOf(10); if(end<0) break;
          const line=buffer.subarray(0,end).toString('ascii');buffer=buffer.subarray(end+1);
          const match=/^([a-f0-9]{40}) blob (\d+)$/.exec(line);
          if(!match || match[1]!==entries[index]?.oid) throw Error(`Unexpected Git blob response: ${line}`);
          header={oid:match[1],size:Number(match[2])};
        }
        if(buffer.length<header.size+1)break;
        const bytes=buffer.subarray(0,header.size);
        if(buffer[header.size]!==10)throw Error('Malformed Git blob separator');
        const actual=createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
        if(actual!==header.oid)throw Error(`Git object hash mismatch: ${header.oid}`);
        consume(entries[index++],bytes);buffer=buffer.subarray(header.size+1);header=null;
      }
    }
    const result=await closed;
    if(failure||result.code!==0||result.signal)throw Error(`Git blob read failed: ${failure?.message??stderr}`);
    if(header||buffer.length||index!==entries.length)throw Error('Incomplete Git blob stream');
  } finally { if(child.exitCode===null)child.kill(); }
}

export async function generateInventory({upstream,pin,output,goCommand=process.env.GO??'go'}) {
  if(!/^[a-f0-9]{40}$/.test(pin??''))throw Error('An exact 40-character upstream commit pin is required');
  upstream=resolve(upstream);output=resolve(output);
  const goVersion=execFileSync(goCommand,['env','GOVERSION'],{encoding:'utf8',stdio:['pipe','pipe','pipe']}).trim();
  if(goVersion!=='go1.27.3')throw Error('Inventory requires the pinned Go 1.27.3 parser');
  if(Number(process.versions.node.split('.')[0])!==24)throw Error('Inventory requires Node 24');
  if(existsSync(output))throw Error('Output directory already exists; choose a new directory to preserve evidence');
  const resolved=command(upstream,['rev-parse','--verify',`${pin}^{commit}`]).toString().trim();
  if(resolved!==pin)throw Error('Upstream pin does not resolve to the requested commit');
  const tree=command(upstream,['rev-parse',`${pin}^{tree}`]).toString().trim();
  const entries=parseTree(command(upstream,['ls-tree','-r','-z','--full-tree',pin]));
  entries.sort((a,b)=>Buffer.compare(Buffer.from(a.path),Buffer.from(b.path)));
  const assets=[],fixtures=[],goInputs=[],jsSources=[];
  const policyPaths=new Set(policies.map(x=>x[1]));
  await readBlobs(upstream,entries.filter(x=>x.type==='blob'),(entry,bytes)=>{
    const asset={id:stableId(pin,'asset',entry.path),...entry,...classifyAsset(entry.path),bytes:bytes.length,sha256:sha256(bytes)};
    if(entry.mode==='120000') {
      asset.role='git-symlink'; asset.linkTargetBase64=bytes.toString('base64');
      try{asset.linkTargetUtf8=decoder.decode(bytes);}catch{asset.linkTargetUtf8=null;}
      asset.expansion={status:'UNEXPANDED',reason:'SYMLINK_NOT_FOLLOWED'};
    } else if(asset.role==='fixture-input') fixtures.push(inspectFixture(asset,bytes));
    else if(['go-test-source','go-test-fixture'].includes(asset.role)||policyPaths.has(entry.path)&&entry.path.endsWith('.go')) {
      try { goInputs.push({path:entry.path,source:decoder.decode(bytes)}); }
      catch { asset.decoding='UTF8_INVALID';asset.rawBytesBase64=bytes.toString('base64');asset.blocker='GO_SOURCE_DECODING_UNIMPLEMENTED'; }
    } else if(['js-test-source','js-benchmark-source'].includes(asset.role)) {
      jsSources.push({id:stableId(pin,'js-source',entry.path),assetId:asset.id,path:entry.path,domain:asset.domain,kind:asset.role,
        expansion:{status:'UNEXPANDED',concreteCases:null,blockers:['JAVASCRIPT_RUNNER_AND_GENERATED_VARIANTS']},progress:initialProgress()});
    }
    assets.push(asset);
  });
  const gitlinks=entries.filter(x=>x.type==='commit').map(entry=>({path:entry.path,commit:entry.oid,expanded:false}));
  for(const entry of entries.filter(x=>x.type==='commit'))assets.push({id:stableId(pin,'asset',entry.path),...entry,role:'gitlink',domain:'unexpanded:gitlinks',expansion:{status:'UNEXPANDED',reason:'SUBMODULE_CHECKOUT_NOT_EXPANDED'}});
  assets.sort((a,b)=>Buffer.compare(Buffer.from(a.path),Buffer.from(b.path)));
  if(assets.length!==entries.length)throw Error('Tracked tree reconciliation failed');
  const assetByPath=new Map(assets.map(x=>[x.path,x]));
  let goSources=[];
  if(goInputs.length) {
    const parsed=spawnSync(goCommand,['run',join(directory,'scope-go/main.go')],{input:goInputs.map(x=>JSON.stringify(x)).join('\n')+'\n',encoding:'utf8',maxBuffer:128*1024*1024});
    if(parsed.error||parsed.status!==0||parsed.signal)throw Error(`Go declaration parser failed: ${parsed.error?.message??parsed.stderr}`);
    goSources=parsed.stdout.trim().split('\n').filter(Boolean).map(x=>JSON.parse(x));
    if(goSources.length!==goInputs.length||goSources.some((x,i)=>x.path!==goInputs[i].path))throw Error('Go source inventory reconciliation failed');
  }
  const declarations=[];
  for(const source of goSources) {
    const asset=assetByPath.get(source.path);
    source.assetId=asset.id;
    if(asset.role!=='go-test-source')continue;
    for(const declaration of source.declarations)declarations.push({
      id:stableId(pin,'go-declaration',[source.path,declaration.name,declaration.line]),assetId:asset.id,path:source.path,domain:asset.domain,...declaration,
      buildConstraints:source.buildConstraints,
      selection:{kind:'STATIC_GO_DECLARATION',runtimeDiscovered:false,buildConstraintsEvaluated:false,filename:basename(source.path)},
      expansion:{status:'UNEXPANDED',concreteCases:null,blockers:[...(source.parseError?['GO_PARSE_ERROR']:[]),'RUNTIME_SUBTEST_AND_HELPER_EXPANSION','BUILD_AND_ENVIRONMENT_PROFILES']},progress:initialProgress(),
    });
  }
  const selectionSource=goSources.find(x=>x.path==='tsc/internal/testrunner/compiler_runner.go');
  const skipList=selectionSource?.stringLists.find(x=>x.name==='skippedTests');
  const skipPolicy=skipList?{assetId:selectionSource.assetId,name:skipList.name,line:skipList.line,complete:skipList.complete,values:skipList.values}:null;
  for(const fixture of fixtures) {
    if(['fixture:compiler','fixture:conformance'].includes(fixture.domain)) {
      if(skipList?.complete&&!selectionSource.parseError) {
        fixture.upstreamSelection.policy={assetId:selectionSource.assetId,declaration:'skippedTests',line:skipList.line};
        if(skipList.values.includes(basename(fixture.path)))fixture.upstreamSelection.status='UPSTREAM_SKIPPED';
      } else {
        fixture.upstreamSelection.status='SELECTION_POLICY_UNRESOLVED';fixture.expansion.blockers.push('COMPILER_SKIP_POLICY_UNRESOLVED');
      }
    }
  }
  const counts=summarize(assets,fixtures,declarations);
  const registry=policies.map(([id,path])=>({id,path,present:assetByPath.has(path),assetId:assetByPath.get(path)?.id??null,
    sha256:assetByPath.get(path)?.sha256??null,expansion:'UNEXPANDED'}));
  const progress={schemaVersion:1,upstreamCommit:pin,releaseEligible:false,scopeComplete:false,concreteCases:null,
    frozenCohort:{separate:true,cases:29,mappingToUpstreamInventory:'UNVERIFIED',creditTowardFullScope:0},
    domains:counts.domains.map(x=>({id:x.id,sourceAssets:x.assets,concreteCases:null,expansion:'UNEXPANDED',...initialProgress()})),
    blockers:['CANONICAL_CONFIGURATION_EXPANSION','VIRTUAL_FILES_AND_BYTE_DECODING','GO_DYNAMIC_SUBTESTS_AND_FUZZ_SEEDS','JS_GENERATED_TESTS_AND_BENCHMARKS','RUNTIME_SKIP_AND_FAILURE_CLASSIFICATION','OS_ARCH_BUILD_RACE_AND_RUNNER_PROFILES','UNCLASSIFIED_SOURCE_AUDIT','COMPILER_REFERENCE_ACCEPTANCE_NOT_RUN',...(gitlinks.length?['GITLINKS_UNEXPANDED']:[])]};
  for(const record of [...fixtures,...declarations,...jsSources])validateProgress(record.progress);
  for(const domain of progress.domains)validateProgress(domain);
  // Write only after all source reading/parsing succeeds. Existing destinations are never overwritten.
  mkdirSync(output,{recursive:true});
  const artifacts=[];
  const save=(path,value,jsonl=false)=>{const bytes=Buffer.from(jsonl?value.map(x=>JSON.stringify(x)).join('\n')+(value.length?'\n':''):JSON.stringify(value,null,2)+'\n');writeFileSync(join(output,path),bytes,{flag:'wx'});artifacts.push({path,bytes:bytes.length,sha256:sha256(bytes)});};
  save('assets.jsonl',assets,true);save('fixtures.jsonl',fixtures,true);save('go-sources.jsonl',goSources,true);save('declarations.jsonl',declarations,true);save('js-sources.jsonl',jsSources,true);
  save('runner-policies.json',{registry,compilerSkipPolicy:skipPolicy});save('progress.json',progress);
  const generatorFiles=['tools/inventory-ts7.mjs','tools/ts7-scope.mjs','tools/scope-go/main.go'];
  const summary={schemaVersion:1,kind:'STATIC_SOURCE_AND_DECLARED_METADATA_INVENTORY',
    provenance:{toolchain:{go:goVersion,nodeMajor:24},repository:'https://github.com/microsoft/TypeScript.git',commit:pin,tree,gitlinks,
      source:'Git committed tree/blob objects; working tree and untracked files ignored',
      generatorFiles:generatorFiles.map(path=>({path,sha256:sha256(readFileSync(join(repository,path)))})),
      hashAlgorithms:{object:'git-sha1',contentAndArtifacts:'sha256'}},
    counts:{...counts,gitSymlinks:assets.filter(x=>x.mode==='120000').length,gitlinks:gitlinks.length,
      goTestSourceFiles:assets.filter(x=>x.role==='go-test-source').length,goTestFixtureFiles:assets.filter(x=>x.role==='go-test-fixture').length,
      goParseErrors:goSources.filter(x=>x.parseError).length,jsTestSources:jsSources.filter(x=>x.kind==='js-test-source').length,jsBenchmarkSources:jsSources.filter(x=>x.kind==='js-benchmark-source').length},
    reconciliation:{gitTreeEntries:entries.length,recordedAssets:assets.length,allTrackedAssetsRecorded:true,testScopeFullyClassified:false},
    artifacts,limitations:progress.blockers};
  writeFileSync(join(output,'summary.json'),JSON.stringify(summary,null,2)+'\n',{flag:'wx'});
  return summary;
}

if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if(!process.argv[2]||process.argv.length>4)throw Error('Usage: node tools/inventory-ts7.mjs UPSTREAM [NEW_OUTPUT_DIRECTORY]');
    const pin=JSON.parse(readFileSync(join(repository,'upstream.lock.json'))).commit;
    const result=await generateInventory({upstream:process.argv[2],pin,output:process.argv[3]??join(repository,'.artifacts/ts7-scope')});
    console.log(`Recorded ${result.counts.trackedAssets} tracked assets at ${pin}. Expanded case total: unknown. Acceptance: NOT_RUN. Release eligible: false.`);
  } catch(error) { console.error(error.message);process.exitCode=1; }
}
