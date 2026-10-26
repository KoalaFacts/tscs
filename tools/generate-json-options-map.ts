import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';import {tmpdir} from 'node:os';
import {spawnSync,execFileSync} from 'node:child_process';import {sha256,stableId} from './ts7-scope.ts';
const [up,out,production]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
if(!up||!out)throw Error('Usage: node tools/generate-json-options-map.ts PIN OUTPUT [DECLARATIONS]');
const upstream=resolve(up),git=(...a:string[])=>execFileSync('git',['-C',upstream,...a]);
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const sourcePath='tests/Tscs.Compiler.Tests/TestData/source-config-values.json',cohort=JSON.parse(readFileSync(sourcePath,'utf8'));
if(sha256(readFileSync(sourcePath))!=='a42df34c1b72c3e42f12f19c7e6894f375afdf63f72c34429f02dbd59e492ae9'||cohort.upstreamCommit!==pin||cohort.vectors.length!==172)throw Error('Selected cohort identity mismatch');
const sourceBindings=cohort.vectors.map(v=>{
 const blob=git('rev-parse',pin+':'+v.source.path).toString().trim(),raw=git('cat-file','blob',blob);
 if(v.source.sourceId!==stableId(stableId(pin,'asset',v.source.path),'fixture',v.source.path)||v.id!==stableId(v.source.sourceId,'source-config-values','default-options/pre-vfs/no-notifier')||v.configId!==stableId(v.id,'config',v.selection.configFile.name+':'+sha256(Buffer.from(v.selection.configFile.content,'base64')))||blob!==v.source.blobOid||sha256(raw)!==v.source.rawSha256||!Buffer.from(v.request.input,'base64').equals(raw)||Buffer.from(v.request.fileName,'base64').toString('utf8')!==v.source.path||v.reference.sourceBytes!==v.selection.configFile.content||v.reference.fileName!==v.context.configPath||v.reference.id!==v.id)throw Error('Selected source provenance mismatch');
 return {id:v.id,source:v.source,configId:v.configId,request:v.request,selection:v.selection,context:v.context};
});
const inputs=cohort.vectors.map(v=>({id:v.id,source:Buffer.from(v.selection.configFile.content,'base64').toString('utf8'),basePath:Buffer.from(v.context.directory,'base64').toString('utf8')}));
const scenarios=['{}','null','[]','{"compilerOptions":null}','{"compilerOptions":false}','{"compilerOptions":1}','{"compilerOptions":"x"}','{"compilerOptions":[]}',
 ...['{"strict":true,"target":"es2015","types":["node"],"module":"commonjs"}','{"noEmit":1,"target":"bad","types":[1,"node",false],"version":null}','{"strict":false,"strict":true,"target":"ES6","strict":null}',
 '{"Strict":true,"NOEMIT":true,"moduleresolution":"node"}','{"stric":true,"noEmi":true,"zzyyxxzz":true,"targett":"es6"}','{"lib":["ES2015","x",1],"moduleSuffixes":["",1,".x"]}',
 '{"outDir":"out","strict":true,"locale":"en"}','{"OutDir":"out","local":"en","rootDirr":"x"}','{"":true,"😀":false,"jsx":false}',
 '{"strict":true,"target":"es2015","lib":null,"types":[],"plugins":[{},false]}'].map(s=>'/*😀*/ {"compilerOptions":'+s+'}')];
scenarios.forEach((source,i)=>inputs.push({id:'map-scenario:'+i,source,basePath:'/.src'}));
// One mixed raw map per declaration locks real production dispatch, not a cartesian helper matrix.
const old=JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/json-scalar-options.json','utf8')).inventory;
for(const [position,x] of old.entries()){const o=x.option;let value=o.kind==='boolean'?'true':o.kind==='number'?'1':o.kind==='object'?'{}':o.kind==='list'?'[]':o.kind==='enum'?'""':'"x"';inputs.push({id:'map-declaration:'+position+'/'+o.name,source:'{"compilerOptions":{'+JSON.stringify(o.name)+':'+value+',"noEmit":true}}',basePath:'/.src'});}
const temp=mkdtempSync(join(tmpdir(),'tscs-options-map-'));
try{
 writeFileSync(join(temp,'input.json'),JSON.stringify(inputs));
 const probes=['config_values_oracle_test.go','json_options_map_oracle_test.go'];
 writeFileSync(join(temp,'overlay.json'),JSON.stringify({Replace:Object.fromEntries(probes.map(p=>[join(upstream,'tsc/internal/tsoptions/tscs_'+p),resolve('tools/fixture-harness',p)]))}));
 const r=spawnSync('go',['test','-overlay',join(temp,'overlay.json'),'-run','^TestTscsJsonOptionsMapOracle$','-count=1','./internal/tsoptions'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOWORK:'off',TSCS_STRING_INPUT:join(temp,'input.json'),TSCS_STRING_OUTPUT:join(temp,'output.json')}});
 if(process.env.TSCS_STRING_LOG)writeFileSync(process.env.TSCS_STRING_LOG,(r.stdout??'')+(r.stderr??''));if(r.error||r.signal||r.status!==0)throw Error(r.error??r.stdout+r.stderr);
 const data=JSON.parse(readFileSync(join(temp,'output.json'),'utf8'));
 if(data.inventory.length!==126||data.vectors.length!==inputs.length||new Set(inputs.map(x=>x.id)).size!==inputs.length||data.vectors.some((v,i)=>Object.keys(inputs[i]).some(k=>JSON.stringify(v[k])!==JSON.stringify(inputs[i][k]))))throw Error('Oracle input binding mismatch');
 const refs=['tsc/internal/tsoptions/tsconfigparsing.go','tsc/internal/tsoptions/commandlineoption.go','tsc/internal/tsoptions/declarations_generated.go','tsc/internal/tsoptions/errors.go','tsc/internal/core/core.go','tsc/internal/parser/parser.go'];
 writeFileSync(out,JSON.stringify({upstreamCommit:pin,goVersion,profile:'convertOptionsFromJson/record-ParseOption/no-host',selectedCohortSha256:sha256(readFileSync(sourcePath)),sourceBindings,referenceHashes:Object.fromEntries(refs.map(p=>[p,sha256(git('show',pin+':'+p))])),sourceHashes:Object.fromEntries(['tools/generate-json-options-map.ts',...probes.map(p=>'tools/fixture-harness/'+p)].map(p=>[p,sha256(readFileSync(p))])),...data})+'\n');
 if(production)writeFileSync(production,JSON.stringify(data.inventory)+'\n');console.log(JSON.stringify({declarations:data.inventory.length,supported:data.inventory.filter(x=>x.supported).length,vectors:data.vectors.length,sourceConfigs:sourceBindings.length}));
}finally{rmSync(temp,{recursive:true,force:true});}
