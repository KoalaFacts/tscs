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
 add(`basic/${prefix}`,[entry(p('a/file.ts'),'x;'),entry(p('b.ts'),''),entry(p('a/sub/file'),'s')],[p(''),p('a'),p('a/sub'),p('a/file.ts'),p('missing')]);
 add(`links/${prefix}`,[entry(p('a/file.ts'),'x;'),entry(p('a/sub/file'),'y'),entry(p('z-file'),p('a/file.ts'),true),entry(p('z-dir'),p('a'),true),entry(p('z-chain'),p('z-dir'),true),entry(p('z-broken'),p('missing'),true)],[p(''),p('z-dir'),p('z-chain'),p('z-broken')]);
 add(`children/${prefix}`,[entry(p('a/real.ts'),'x'),entry(p('a/sub/file'),'y'),entry(p('a/file-link'),p('a/real.ts'),true),entry(p('a/dir-link'),p('a/sub'),true),entry(p('a/broken'),p('missing'),true),entry(p('a/link-chain'),p('a/dir-link'),true)],[p('a'),p('a/dir-link')]);
 add(`ordering/${prefix}`,[entry(p('a/Z'),'x'),entry(p('a/a'),'x'),entry(p('a/A'),'x'),entry(p('a/a-dir/file'),'x'),entry(p('a/z-dir/file'),'x')],[p('a')]);
}
add('empty',[],['/','/missing']);
add('linked-parent',[entry('/a-real/file','x'),entry('/z-alias','/a-real',true),entry('/z-alias/sub/file','shadow')],['/','/z-alias','/z-alias/sub','/a-real','/a-real/sub']);
add('part-order',[entry('/a-b','/a',true),entry('/a-b/file','shadow'),entry('/a/file','target')],['/','/a-b','/a']);
add('relative-query',[entry('/a','x')],['a']);
add('invalid-target',[entry('/alias',Buffer.from([47,128]),true),entry(Buffer.from([47,128]),'x')],['/']);
add('invalid-alias',[entry(Buffer.from([47,128]),'/target',true),entry('/target','x')],['/']);
for(let i=0;i<256;i++){
 const name=Buffer.concat([Buffer.from('/dir/name'),Buffer.from([i]),Buffer.from('x')]);
 add(`byte-name/${i}`,[entry(name,'bytes')],['/','/dir']);
 add(`byte-link/${i}`,[entry(name,'/target',true),entry('/target/child','x')],['/dir']);
}
add('nested-broken-shadow-file',[entry('/dir/link','/missing',true),entry('/dir/missing','shadow')],['/dir','/dir/link']);
add('nested-broken-shadow-directory',[entry('/dir/link','/missing',true),entry('/dir/missing/child','shadow')],['/dir','/dir/link']);
add('intermediate-file-link',[entry('/a/file','x'),entry('/z','/a/file',true),entry('/z/shadow/file','y')],['/z']);
add('nested-intermediate-file-link',[entry('/a/file','x'),entry('/dir/z','/a/file',true),entry('/dir/z/shadow/file','y')],['/dir/z']);
// MapFS.Open validates the custom-resolved byte path before its parent-link pass.
// Direct children under an invalid UTF8 alias must not become readable via that pass.
for(const [id,alias] of [['invalid-80',Buffer.from([47,128])],['invalid-ff',Buffer.from([47,255])],['valid-utf8',Buffer.from([47,194,128])],['ascii',Buffer.from('/z-alias')]]){
 const child=Buffer.concat([alias,Buffer.from('/file')]);
 add(`open-validation/${id}`,[entry('/real/file','target'),entry(alias,'/real',true),entry(child,'shadow'),entry('/zz-link',child,true)],[child,alias,'/real/file','/zz-link','/']);
}
const temp=mkdtempSync(join(tmpdir(),'tscs-state-'));
try{
 const input=join(temp,'input.json'),result=join(temp,'output.json'),overlay=join(temp,'overlay.json');writeFileSync(input,JSON.stringify(rows));
 const probe=resolve('tools/fixture-harness/entries_oracle_test.go');writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/testrunner/tscs_entries_oracle_test.go')]:probe,[join(upstream,'tsc/internal/testrunner/tscs_vfs_oracle_test.go')]:resolve('tools/fixture-harness/vfs_oracle_test.go'),[join(upstream,'tsc/internal/testrunner/tscs_state_oracle_test.go')]:resolve('tools/fixture-harness/state_oracle_test.go')}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsEntriesOracle$','-count=1','./internal/testrunner'],{cwd:join(upstream,'tsc'),encoding:'utf8',env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_STATE_INPUT:input,TSCS_STATE_OUTPUT:result}});
 if(run.status!==0)throw Error(run.stdout+run.stderr);console.log(run.stdout.trim());const vectors=JSON.parse(readFileSync(result,'utf8'));if(vectors.length!==rows.length)throw Error('Lost vectors');
 const files=['tsc/internal/vfs/vfstest/vfstest.go','tsc/internal/vfs/iovfs/iofs.go','tsc/internal/vfs/internal/internal.go','tsc/internal/testrunner/compiler_runner.go','tsc/internal/tsoptions/tsoptionstest/vfs.go','tsc/internal/testrunner/test_case_parser.go','tsc/internal/scanner/scanner.go','tsc/internal/stringutil/util.go','tsc/internal/tspath/path.go','tsc/internal/vfs/internal/internal.go'];
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion,sourceHashes:Object.fromEntries(files.map(p=>[p,createHash('sha256').update(git('show',`${pin}:${p}`)).digest('hex')])),stateProbeSha256:createHash('sha256').update(readFileSync('tools/fixture-harness/state_oracle_test.go')).digest('hex'),vfsProbeSha256:createHash('sha256').update(readFileSync('tools/fixture-harness/vfs_oracle_test.go')).digest('hex'),probeSha256:createHash('sha256').update(readFileSync(probe)).digest('hex'),vectors},null,2)+'\n');console.log(`Executed actual Go entries for ${vectors.length} vectors`);
}finally{rmSync(temp,{recursive:true,force:true});}
