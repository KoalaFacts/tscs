import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',upstream=resolve(process.argv[2]??''),output=process.argv[3];
if(!process.argv[2]||!output)throw Error('Usage: node tools/generate-fixture-map-vectors.ts CHECKED_OUT_UPSTREAM OUTPUT');
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
if(git('rev-parse','HEAD').trim()!==pin||git('status','--porcelain','--untracked-files=no').trim())throw Error('Exact clean reference checkout required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
if(JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Reference pin requires separate review');
const rows=[];const add=(id,text)=>rows.push({id,input:Buffer.from(text).toString('base64'),fileName:Buffer.from('test.ts').toString('base64'),implicit:false,failAt:0,returnNil:false});
for(const dir of ['', '/project','C:/src','file:///c:/src','http://host/src','^/~ts-uri~/s/auth/dir','../base'])for(const name of ['a.ts','../a.ts','x/../a.ts','/root.ts','C:/root.ts','a?x'])add(`map/${dir}/${name}`,`${dir?'// @currentDirectory: '+dir+'\n':''}// @filename: ${name}\nx;\n// @symlink: ../alias.ts\n// @link: a.ts -> second\n// @filename: ./a.ts\ny;`);
add('permissive-links','// @currentDirectory: http://host/src\n// @link: -> ?q#f\n// @link: #target -> \n// @filename: a.ts\nx;');
add('empty','');add('duplicates','// @filename: a.ts\nx;\n// @filename: ./a.ts\ny;');add('link-collision','// @link: a -> x\n// @link: b -> ./x\n// @filename: a.ts\nx;');
for(const row of JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/source-decoding.json','utf8')).vectors.filter(r=>r.id.startsWith('fixture:')))rows.push({id:row.id,input:execFileSync('git',['-C',upstream,'show',`${pin}:${row.id.slice(8)}`]).toString('base64'),fileName:Buffer.from(row.id.slice(8)).toString('base64'),implicit:false,failAt:0,returnNil:false});
rows.push({id:'invalid-name',input:Buffer.from('// @filename: a/\x80.ts\n// @symlink: \x81.ts\nx;', 'latin1').toString('base64'),fileName:'YS50cw==',implicit:false,failAt:0,returnNil:false});
const temp=mkdtempSync(join(tmpdir(),'tscs-state-'));
try{
 const input=join(temp,'input.json'),result=join(temp,'output.json'),overlay=join(temp,'overlay.json');writeFileSync(input,JSON.stringify(rows));
 const probe=resolve('tools/fixture-harness/map_oracle_test.go');writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_map_oracle_test.go')]:probe,[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:resolve('tools/fixture-harness/state_oracle_test.go')}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsFileMapOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_STATE_INPUT:input,TSCS_STATE_OUTPUT:result}});
 if(run.status!==0)throw Error(run.stdout+run.stderr);console.log(run.stdout.trim());const vectors=JSON.parse(readFileSync(result,'utf8'));if(vectors.length!==rows.length)throw Error('Lost vectors');
 const files=['tsc/internal/testrunner/compiler_runner.go','tsc/internal/tsoptions/tsoptionstest/vfs.go','tsc/internal/testrunner/test_case_parser.go','tsc/internal/scanner/scanner.go','tsc/internal/stringutil/util.go','tsc/internal/tspath/path.go','tsc/internal/vfs/internal/internal.go'];
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion,sourceHashes:Object.fromEntries(files.map(p=>[p,createHash('sha256').update(git('show',`${pin}:${p}`)).digest('hex')])),stateProbeSha256:createHash('sha256').update(readFileSync('tools/fixture-harness/state_oracle_test.go')).digest('hex'),probeSha256:createHash('sha256').update(readFileSync(probe)).digest('hex'),vectors},null,2)+'\n');console.log(`Executed actual Go file map for ${vectors.length} vectors`);
}finally{rmSync(temp,{recursive:true,force:true});}
