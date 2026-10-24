import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',upstream=resolve(process.argv[2]??''),output=process.argv[3];
if(!process.argv[2]||!output)throw Error('Usage: node tools/generate-fixture-state-vectors.ts CHECKED_OUT_UPSTREAM OUTPUT');
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
if(git('rev-parse','HEAD').trim()!==pin||git('status','--porcelain','--untracked-files=no').trim())throw Error('Exact clean reference checkout required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
if(JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Reference pin requires separate review');
const sources={empty:'',blank:'\n\n',single:'\n\nx;\r\n',emptyFiles:'// @filename: a.ts\n// @filename: b.ts\n',files:'// @strict: false\n// @filename: a.ts\n// @noOpen: true\nx;\n// @filename: b.ts\n// @strict: true;\ny;',links:'// @symlink: early\n// @link: real -> alias\n// @filename: a.ts\n// @symlink: b.ts, c.ts,\nx;\n// @filename: d.ts\n// @symlink: b.ts\n// @currentDirectory: /p',prelude:'// comment\n/* more */\n// @filename: a.ts\nx;',codePrelude:'x;\n// @filename: a.ts\ny;',shebang:'#!node\n// @filename: a.ts\nx;',unterminated:'/*\n// @filename: a.ts\nx;',unicode:'\u0085\u200b\ufeff\u2028\n// @filename: a.ts\nx;',conflict:'<<<<<<< ours\n=======\nignored\n>>>>>>> theirs\n// @filename: a.ts\nx;',markerLookbehind:'// c\n  =======\nignored\n// @filename: a.ts\nx;',unicodeMarker:'\u2028=======\nignored\n// @filename: a.ts\nx;',emptyName:'// @filename:\n\n// @filename: b.ts\nx;',midError:'// @filename: a.ts\nx;\n// @filename: b.ts\ny;\n// @filename: c.ts\nz;'};
const rows=[];const add=(id,bytes,fileName='folder/test.ts',implicit=false,failAt=0,returnNil=false)=>rows.push({id,input:Buffer.from(bytes).toString('base64'),fileName:Buffer.from(fileName).toString('base64'),implicit,failAt,returnNil});
for(const [id,text] of Object.entries(sources))for(const implicit of [false,true])for(const failAt of [0,1,2,3])add(`${id}/${implicit}/${failAt}`,Buffer.from(text),'folder/test.ts',implicit,failAt);
for(const fileName of ['a.ts','dir/a.ts/','/','C:','C:/','C:abc','//server','//server/','file:///c:/','http://host/','^/untitled/ts-nul-authority/name'])add(`basename/${fileName}`,Buffer.from('x;'),fileName);
add('unicode-localhost',Buffer.from('x;'),'file://localhoſt/c:');
add('nil-eof',Buffer.from('x;'),'a.ts',false,1,true);
add('nil-boundary',Buffer.from('// @filename: a.ts\nx;\n// @filename: b.ts'),'a.ts',false,1,true);
for(const row of JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/source-decoding.json','utf8')).vectors.filter(r=>r.id.startsWith('fixture:')))add(row.id,execFileSync('git',['-C',upstream,'show',`${pin}:${row.id.slice(8)}`]));
add('raw-names',Buffer.from('// @filename: \x80.ts\n// @symlink: \x81.ts\n/\x82/u\n','latin1'));
const temp=mkdtempSync(join(tmpdir(),'tscs-state-'));
try{
 const input=join(temp,'input.json'),result=join(temp,'output.json'),overlay=join(temp,'overlay.json');writeFileSync(input,JSON.stringify(rows));
 const probe=resolve('tools/fixture-harness/state_oracle_test.go');writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:probe}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsFixtureStateOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_STATE_INPUT:input,TSCS_STATE_OUTPUT:result}});
 if(run.status!==0)throw Error(run.stdout+run.stderr);console.log(run.stdout.trim());const vectors=JSON.parse(readFileSync(result,'utf8'));if(vectors.length!==rows.length)throw Error('Lost vectors');
 const files=['tsc/internal/testrunner/test_case_parser.go','tsc/internal/scanner/scanner.go','tsc/internal/stringutil/util.go','tsc/internal/tspath/path.go','tsc/internal/vfs/internal/internal.go'];
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion,sourceHashes:Object.fromEntries(files.map(p=>[p,createHash('sha256').update(git('show',`${pin}:${p}`)).digest('hex')])),probeSha256:createHash('sha256').update(readFileSync(probe)).digest('hex'),vectors},null,2)+'\n');console.log(`Executed actual Go fixture parser for ${vectors.length} vectors`);
}finally{rmSync(temp,{recursive:true,force:true});}
