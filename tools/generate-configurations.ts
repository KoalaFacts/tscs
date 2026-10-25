import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {tmpdir} from 'node:os';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [up,output]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
if(!up||!output)throw Error('Usage: node tools/generate-configurations.ts PINNED_CHECKOUT OUTPUT');
const upstream=resolve(up),git=(...args)=>execFileSync('git',['-C',upstream,...args]);
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const version=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!version.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const temp=mkdtempSync(join(tmpdir(),'tscs-options-'));try{
 const existing=JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/option-values.json','utf8'));
 const requests=existing.vectors.map(r=>({id:'single/'+r.id,settings:{[r.option]:r.value},varyBy:[r.option]}));
 const add=(id,settings,varyBy)=>requests.push({id,settings,varyBy});
 add('empty',{},[]);add('scalar',{filename:'../a.ts',target:'ESNEXT, ES2015'},[]);
 add('empty-vary',{strict:''},['strict']);add('mixed-empty',{strict:'',filename:'a.ts'},['strict']);
 add('vary-case-sensitive',{Target:'invalid'},['target']);add('vary-fold-name',{STRICT:'TRUE,false'},['STRICT']);
 for(let n=1;n<=6;n++){const names=['strict','noEmit','noLib','declaration','sourceMap','allowJs'].slice(0,n);add('bool-product/'+n,Object.fromEntries(names.map(k=>[k,'true,false'])),names);}
 for(const n of [4,5,6])add('cap/'+n,{outDir:Array.from({length:n},(_,i)=>'d'+i).join(','),outFile:'a,b,c,d,e'},['outDir','outFile']);
 add('lowercase-name',{moduleResolution:'classİc,node16',filename:'A.ts'},['moduleResolution']);
 add('same-description',{outDir:'A,a'},['outDir']);add('empty-removal',{strict:'*,-true,-false'},['strict']);
 const input=join(temp,'input.json');writeFileSync(input,JSON.stringify(requests));
 const probe=resolve('tools/fixture-harness/configurations_oracle_test.go'),result=join(temp,'result.json'),overlay=join(temp,'overlay.json');
 writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_configurations_test.go')]:probe}}));
 const run=spawnSync('go',['test','-json','-overlay',overlay,'-run','^TestTscsConfigurationsOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,maxBuffer:32*1024*1024,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',TSCS_CONFIG_OUTPUT:result,TSCS_CONFIG_INPUT:input}});
 if(run.error||run.signal||![0,1].includes(run.status))throw Error(run.error??run.stderr);
 const data=JSON.parse(readFileSync(result,'utf8'));const vectors=data.vectors;const tableRequests=Object.entries(data.lowercase).map(([from,to])=>({id:'unicode/'+Number(from).toString(16).padStart(6,'0'),settings:{outDir:String.fromCodePoint(Number(from))+','+String.fromCodePoint(to)},varyBy:['outDir']})).sort((a,b)=>a.id<b.id?-1:a.id>b.id?1:0);requests.push(...tableRequests);if(vectors.length!==requests.length)throw Error('Lost vectors');for(let i=0;i<vectors.length;i++){if(vectors[i].id!==requests[i].id||JSON.stringify(Object.entries(vectors[i].settings).sort())!==JSON.stringify(Object.entries(requests[i].settings).sort())||JSON.stringify(vectors[i].varyBy)!==JSON.stringify(requests[i].varyBy))throw Error('Input binding mismatch');}const fatal=data.vectors.filter(r=>r.expected.fatal).length;
 if((run.status===1)!==(fatal>0))throw Error('Go exit does not reconcile to intentional fatal vectors');
 const events=run.stdout.trim().split('\n').filter(Boolean).map(line=>JSON.parse(line));
 const expectedFailures=new Set(data.vectors.filter(r=>r.expected.fatal).map(r=>'TestTscsConfigurationsOracle/'+r.id));
 if(fatal)expectedFailures.add('TestTscsConfigurationsOracle');
 const failed=new Set(events.filter(e=>e.Action==='fail'&&e.Test).map(e=>e.Test));
 if(failed.size!==expectedFailures.size||[...failed].some(t=>!expectedFailures.has(t)))throw Error('Unexpected Go test failure');
 if(process.env.TSCS_CONFIG_LOG)writeFileSync(process.env.TSCS_CONFIG_LOG,run.stdout+run.stderr);
 if(new Set(data.vectors.map(r=>r.id)).size!==data.vectors.length)throw Error('Duplicate vector identities');

 if(process.argv[4])writeFileSync(process.argv[4],JSON.stringify({goVersion:version,unicodeVersion:data.unicodeVersion,lowercase:data.lowercase},null,2)+'\n');
 const sources=['tsc/internal/testutil/harnessutil/harnessutil.go','tsc/internal/tsoptions/declarations_generated.go'];
 const sha=b=>createHash('sha256').update(b).digest('hex');
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion:version,referenceExit:run.status,expectedFatalVectors:fatal,expectedGoFailuresVerified:true,unicodeTableSourceSha256:sha(readFileSync(join(execFileSync('go',['env','GOROOT'],{encoding:'utf8'}).trim(),'src/unicode/tables.go'))),inputOptionValuesSha256:sha(readFileSync('tests/Tscs.Compiler.Tests/TestData/option-values.json')),sourceHashes:Object.fromEntries(sources.map(p=>[p,sha(git('show',`${pin}:${p}`))])),probeSha256:sha(readFileSync(probe)),...data},null,2)+'\n');
 console.log(JSON.stringify({vectors:data.vectors.length,intentionalFatalVectors:fatal,referenceExit:run.status}));
}finally{rmSync(temp,{recursive:true,force:true});}
