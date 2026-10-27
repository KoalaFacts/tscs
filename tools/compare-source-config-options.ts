import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';import {resolve,join} from 'node:path';
import {spawnSync,execFileSync} from 'node:child_process';import {isDeepStrictEqual} from 'node:util';
import {sha256} from './ts7-scope.ts';import {reconcileSourceOptions} from './source-config-options.ts';
const [pin,output]=process.argv.slice(2);if(!pin||!output)throw Error('Usage: node tools/compare-source-config-options.ts PIN NEW_OUTPUT');
const out=resolve(output);mkdirSync(out);const put=(p:string,b:string|Buffer)=>writeFileSync(join(out,p),b,{flag:'wx',flush:true});
const oracle=spawnSync(process.execPath,['tools/generate-json-options-map.ts',pin,join(out,'oracle.json'),join(out,'declarations.json')],{encoding:'utf8',timeout:180000,env:{...process.env,TSCS_STRING_LOG:join(out,'go.log')}});
put('generator.log',(oracle.stdout??'')+(oracle.stderr??''));if(oracle.error||oracle.signal||oracle.status!==0)throw Error(oracle.error??'Go oracle failed');
if(!readFileSync(join(out,'declarations.json')).equals(readFileSync('src/Tscs.Compiler/json-option-declarations.json')))throw Error('Product declarations differ from actual pinned Go');
const data=JSON.parse(readFileSync(join(out,'oracle.json'),'utf8')),references=new Map(data.vectors.map(v=>[v.id,v]));
const requests=data.sourceBindings.map(b=>({...b.request,profile:'config-options'}));put('requests.jsonl',requests.map(r=>JSON.stringify(r)).join('\n')+'\n');
const actuals:any[]=[],calls:any[]=[],deadline=Date.now()+180000;
for(let offset=0;offset<requests.length;offset+=32){const batch=requests.slice(offset,offset+32),input=batch.map(x=>JSON.stringify(x)).join('\n')+'\n';
 const r=spawnSync(process.env.DOTNET??'dotnet',['tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll'],{input,encoding:'utf8',timeout:Math.max(1,deadline-Date.now()),maxBuffer:16*1024*1024});
 const name='actual-'+String(offset/32).padStart(2,'0')+'.jsonl',log='actual-'+String(offset/32).padStart(2,'0')+'.log';
 put(name,r.stdout??'');put(log,r.stderr??'');if(r.error||r.signal||r.status!==0)throw Error(r.error??'C# profile failed');
 const rows=r.stdout.trimEnd().split('\n').map(x=>JSON.parse(x));if(rows.length!==batch.length||rows.some((x,i)=>x.id!==batch[i].id||Object.keys(x).sort().join(',')!=='expected,id'))throw Error('Actual batch identity mismatch');
 calls.push({offset,count:batch.length,ids:batch.map(x=>x.id),status:r.status,stdoutSha256:sha256(Buffer.from(r.stdout)),stdout:name,stderr:log});actuals.push(...rows);
}
put('actual-calls.json',JSON.stringify(calls)+'\n');
const results=data.sourceBindings.map((b,i)=>{const reference:any=references.get(b.id),actual=actuals[i];let status='MISMATCH',error:string|null=null;try{status=reconcileSourceOptions(b,reference,actual.expected);}catch(e){error=String(e);}
 return {id:b.id,sourceId:b.source.sourceId,configId:b.configId,profile:data.profile,upstreamSkipped:b.source.upstreamSelection.status==='UPSTREAM_SKIPPED',status,error,referenceSha256:sha256(JSON.stringify(reference)),actualSha256:sha256(JSON.stringify(actual)),unsupportedOptions:reference.unsupported,parserDiagnostics:reference.parserDiagnostics,optionDiagnostics:reference.diagnostics};});
put('results.jsonl',results.map(x=>JSON.stringify(x)).join('\n')+'\n');
const counts=Object.fromEntries(['MATCH','MATCH_DIAGNOSTICS','UNSUPPORTED','MISMATCH','CRASH','TIMEOUT','SKIPPED','NOT_RUN'].map(k=>[k,results.filter(x=>x.status===k).length]));
const files=['oracle.json','declarations.json','go.log','generator.log','requests.jsonl','actual-calls.json','results.jsonl',...calls.flatMap(x=>[x.stdout,x.stderr])];
const sources=execFileSync('git',['ls-files','--cached','--others','--exclude-standard','src','tools','tests/Tscs.Compiler.Tests','upstream.lock.json'],{encoding:'utf8'}).trim().split('\n').filter(p=>/\.(cs|csproj|ts|go|json)$/.test(p));
for(const p of ['src/Tscs.Compiler/JsonOptionsFromJson.cs','src/Tscs.Compiler/json-option-declarations.json','tools/fixture-harness/FixtureConfigOptions.cs','tools/fixture-harness/json_options_map_oracle_test.go','tools/generate-json-options-map.ts','tools/source-config-options.ts','tools/compare-source-config-options.ts'])if(!sources.includes(p))sources.push(p);
const summary={upstreamCommit:data.upstreamCommit,sourceCommit:execFileSync('git',['rev-parse','HEAD'],{encoding:'utf8'}).trim(),profile:data.profile,selectedCohortSha256:data.selectedCohortSha256,scope:'historical selected172 identities/bytes; fresh Go parser + convertOptionsFromJson and C# fixture preparation; not full13006 selection, typed options, host or compiler acceptance',counts,sourceHashes:Object.fromEntries(sources.map(p=>[p,sha256(readFileSync(p))])),assemblyHashes:Object.fromEntries(['tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll','tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.Compiler.dll'].map(p=>[p,sha256(readFileSync(p))])),artifacts:files.map(path=>({path,bytes:readFileSync(join(out,path)).length,sha256:sha256(readFileSync(join(out,path)))}))};
put('summary.json',JSON.stringify(summary,null,2)+'\n');
// Immediately reread all Node-owned immutable output, rejecting truncation.
for(const a of summary.artifacts){const b=readFileSync(join(out,a.path));if(b.length!==a.bytes||sha256(b)!==a.sha256)throw Error('Post-completion artifact mismatch: '+a.path);}
if(!isDeepStrictEqual(actuals.map(x=>x.id),requests.map(x=>x.id)))throw Error('Actual coverage mismatch');
console.log(JSON.stringify(counts));process.exitCode=results.some(x=>x.status!=='MATCH')?1:0;
