import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';import {tmpdir} from 'node:os';
import {spawnSync,execFileSync} from 'node:child_process';import {sha256} from './ts7-scope.ts';
const [up,out]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
if(!up||!out)throw Error('Usage: node tools/generate-json-enum-options.ts PIN OUTPUT');
const upstream=resolve(up),git=(...a)=>execFileSync('git',['-C',upstream,...a]);
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const sources=['null','true','0','{}','[]','""','"bogus"','" es6 "','"classİc"','"classıc"','"😀"','"\\ud800"'];
const temp=mkdtempSync(join(tmpdir(),'tscs-enum-options-'));
try {
 writeFileSync(join(temp,'input.json'),JSON.stringify(sources));
 const probes=['config_values_oracle_test.go','json_enum_options_oracle_test.go'];
 writeFileSync(join(temp,'overlay.json'),JSON.stringify({Replace:Object.fromEntries(probes.map(p=>[join(upstream,'tsc/internal/tsoptions/tscs_'+p),resolve('tools/fixture-harness',p)]))}));
 const r=spawnSync('go',['test','-overlay',join(temp,'overlay.json'),'-run','^TestTscsJsonEnumOptionsOracle$','-count=1','./internal/tsoptions'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',TSCS_STRING_INPUT:join(temp,'input.json'),TSCS_STRING_OUTPUT:join(temp,'output.json')}});
 if(process.env.TSCS_STRING_LOG)writeFileSync(process.env.TSCS_STRING_LOG,(r.stdout??'')+(r.stderr??''));
 if(r.error||r.signal||r.status!==0)throw Error(r.error??r.stdout+r.stderr);
 const data=JSON.parse(readFileSync(join(temp,'output.json'),'utf8'));
 const expected=data.inventory.filter(x=>x.supported).flatMap(o=>[...sources,...o.option.entries.flatMap(e=>[JSON.stringify(e.key),JSON.stringify(e.key.toUpperCase())])].flatMap((literal,i)=>[false,true].map(located=>({id:`${o.position}/${o.option.name}/${i}/${located}`,option:o.option,literal,located,source:'/*😀*/ {"compilerOptions": {'+JSON.stringify(o.option.name)+': /*x*/ '+literal+'}}'}))));
 if(data.inventory.length!==126||expected.length!==data.vectors.length||new Set(data.vectors.map(v=>v.id)).size!==expected.length||data.vectors.some((v,i)=>Object.keys(expected[i]).some(k=>JSON.stringify(v[k])!==JSON.stringify(expected[i][k]))))throw Error('Oracle input binding mismatch');
 const paths=['tsc/internal/tsoptions/tsconfigparsing.go','tsc/internal/tsoptions/commandlineoption.go','tsc/internal/tsoptions/declarations_generated.go','tsc/internal/tsoptions/errors.go','tsc/internal/parser/parser.go','tsc/internal/tsoptions/commandlineparser.go'];
 writeFileSync(out,JSON.stringify({upstreamCommit:pin,goVersion,sources,referenceHashes:Object.fromEntries(paths.map(p=>[p,sha256(git('show',pin+':'+p))])),sourceHashes:Object.fromEntries(['tools/generate-json-enum-options.ts',...probes.map(p=>'tools/fixture-harness/'+p)].map(p=>[p,sha256(readFileSync(p))])),...data})+'\n');
 if(process.argv[4])writeFileSync(process.argv[4],JSON.stringify(data.inventory.filter(x=>x.supported).map(x=>x.option))+'\n');
 console.log(JSON.stringify({declarations:data.inventory.length,supported:data.inventory.filter(x=>x.supported).length,vectors:data.vectors.length}));
} finally {rmSync(temp,{recursive:true,force:true});}
