import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {execFileSync,spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',upstream=resolve(process.argv[2]??''),output=process.argv[3];
if(!process.argv[2]||!output)throw Error('Usage: node tools/generate-vfs-vectors.ts CHECKED_OUT_UPSTREAM OUTPUT');
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{encoding:'utf8',env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
if(git('rev-parse','HEAD').trim()!==pin||git('status','--porcelain','--untracked-files=no').trim())throw Error('Exact clean reference checkout required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
if(JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Reference pin requires separate review');
const rows=[],b64=x=>Buffer.from(x).toString('base64'),entry=(path,data,link=false)=>({path:b64(path),data:b64(data),link});
const add=(id,entries,queries)=>rows.push({id,entries,queries:queries.map(b64)});
for(const prefix of ['/','C:/']){
 const p=x=>prefix+x;
 add(`basic/${prefix}`,[entry(p('a/file.ts'),'x;'),entry(p('b.ts'),'')],[p(''),p('a'),p('a/file.ts'),p('a/missing'),p('a/file.ts/child'),p('b.ts')]);
 add(`links/${prefix}`,[entry(p('a/file.ts'),'x;'),entry(p('z-file'),p('a/file.ts'),true),entry(p('z-dir'),p('a'),true),entry(p('z-chain'),p('z-file'),true),entry(p('z-broken'),p('missing'),true)],[p('z-file'),p('z-dir'),p('z-dir/file.ts'),p('z-chain'),p('z-broken'),p('z-dir/missing'),p('z-dirx/file.ts')]);
 add(`decode/${prefix}`,[entry(p('a.ts'),Buffer.from([255,254,120,0,59,0,1])),entry(p('b.ts'),Buffer.from([239,187,191,239,187,191,120]))],[p('a.ts'),p('b.ts')]);
 add(`case/${prefix}`,[entry(p('A.ts'),'upper'),entry(p('a.ts'),'lower')],[p('A.ts'),p('a.ts'),p('missing')]);
 add(`file-parent/${prefix}`,[entry(p('a'),'x'),entry(p('a/x'),'y')],[p('a')]);
}
add('empty',[],['/','/missing']);add('file-over-link',[entry('/a','real'),entry('/a','/b',true),entry('/b','target')],['/a']);
add('linked-parent',[entry('/a-real/file','x'),entry('/z-alias','/a-real',true),entry('/z-alias/sub/file','shadow')],['/z-alias/file','/z-alias/sub','/a-real/sub','/z-alias/sub/file']);
for(let i=128;i<256;i++){const name=Buffer.concat([Buffer.from('/a/'),Buffer.from([i]),Buffer.from('.ts')]);add(`raw-name/${i}`,[entry(name,Buffer.from([i]))],[name]);}
add('part-order',[entry('/a-b','/a',true),entry('/a-b/file','shadow'),entry('/a/file','target')],['/a-b/file']);
add('invalid-target',[entry('/alias',Buffer.from([47,128]),true),entry(Buffer.from([47,128]),'x')],['/alias']);
add('invalid-alias',[entry(Buffer.from([47,128]),'/target',true),entry('/target','x')],[Buffer.from([47,128])]);
add('relative-query',[entry('/a','x')],['a']);
add('file-link-parent',[entry('/a','x'),entry('/b','/a',true),entry('/b/x','y')],['/b/x']);
const temp=mkdtempSync(join(tmpdir(),'tscs-state-'));
try{
 const input=join(temp,'input.json'),result=join(temp,'output.json'),overlay=join(temp,'overlay.json');writeFileSync(input,JSON.stringify(rows));
 const probe=resolve('tools/fixture-harness/vfs_oracle_test.go');writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_vfs_oracle_test.go')]:probe,[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:resolve('tools/fixture-harness/state_oracle_test.go')}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsVfsOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_STATE_INPUT:input,TSCS_STATE_OUTPUT:result}});
 if(run.status!==0)throw Error(run.stdout+run.stderr);console.log(run.stdout.trim());const vectors=JSON.parse(readFileSync(result,'utf8'));if(vectors.length!==rows.length)throw Error('Lost vectors');
 const files=['tsc/internal/vfs/vfstest/vfstest.go','tsc/internal/vfs/iovfs/iofs.go','tsc/internal/vfs/internal/internal.go','tsc/internal/testrunner/compiler_runner.go','tsc/internal/tsoptions/tsoptionstest/vfs.go','tsc/internal/testrunner/test_case_parser.go','tsc/internal/scanner/scanner.go','tsc/internal/stringutil/util.go','tsc/internal/tspath/path.go','tsc/internal/vfs/internal/internal.go'];
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion,sourceHashes:Object.fromEntries(files.map(p=>[p,createHash('sha256').update(git('show',`${pin}:${p}`)).digest('hex')])),stateProbeSha256:createHash('sha256').update(readFileSync('tools/fixture-harness/state_oracle_test.go')).digest('hex'),probeSha256:createHash('sha256').update(readFileSync(probe)).digest('hex'),vectors},null,2)+'\n');console.log(`Executed actual Go VFS for ${vectors.length} vectors`);
}finally{rmSync(temp,{recursive:true,force:true});}
