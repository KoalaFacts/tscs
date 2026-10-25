import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',upstream=resolve(process.argv[2]??''),output=process.argv[3];
if(!process.argv[2]||!output)throw Error('Usage: node tools/generate-construction-vectors.ts CHECKED_OUT_UPSTREAM OUTPUT');
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
if(git('rev-parse','HEAD').trim()!==pin||git('status','--porcelain','--untracked-files=no').trim())throw Error('Exact clean reference checkout required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
if(JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Reference pin requires separate review');
const rows=JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/fixture-map.json','utf8')).vectors.map(({id,input,fileName})=>({id,input,fileName,implicit:false,failAt:0,returnNil:false}));
const temp=mkdtempSync(join(tmpdir(),'tscs-state-'));
try{
 const input=join(temp,'input.json'),result=join(temp,'output.json'),overlay=join(temp,'overlay.json');writeFileSync(input,JSON.stringify(rows));
 const probe=resolve('tools/fixture-harness/construction_oracle_test.go');writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_construction_oracle_test.go')]:probe,[join(upstream,'tsc/internal/testrunner/tscs_map_oracle_test.go')]:resolve('tools/fixture-harness/map_oracle_test.go'),[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:resolve('tools/fixture-harness/state_oracle_test.go')}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsConstructionOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_STATE_INPUT:input,TSCS_STATE_OUTPUT:result}});
 if(run.status!==0)throw Error(run.stdout+run.stderr);console.log(run.stdout.trim());const vectors=JSON.parse(readFileSync(result,'utf8'));if(vectors.length!==rows.length)throw Error('Lost vectors');
 const files=['tsc/internal/vfs/vfstest/vfstest.go','tsc/internal/vfs/iovfs/iofs.go','tsc/internal/testrunner/compiler_runner.go','tsc/internal/tsoptions/tsoptionstest/vfs.go','tsc/internal/testrunner/test_case_parser.go','tsc/internal/scanner/scanner.go','tsc/internal/stringutil/util.go','tsc/internal/tspath/path.go','tsc/internal/vfs/internal/internal.go'];
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion,inputVectorsSha256:createHash('sha256').update(readFileSync('tests/Tscs.Compiler.Tests/TestData/fixture-map.json')).digest('hex'),sourceHashes:Object.fromEntries(files.map(p=>[p,createHash('sha256').update(git('show',`${pin}:${p}`)).digest('hex')])),mapProbeSha256:createHash('sha256').update(readFileSync('tools/fixture-harness/map_oracle_test.go')).digest('hex'),stateProbeSha256:createHash('sha256').update(readFileSync('tools/fixture-harness/state_oracle_test.go')).digest('hex'),probeSha256:createHash('sha256').update(readFileSync(probe)).digest('hex'),vectors},null,2)+'\n');console.log(`Executed actual Go fixture VFS for ${vectors.length} vectors`);
}finally{rmSync(temp,{recursive:true,force:true});}
