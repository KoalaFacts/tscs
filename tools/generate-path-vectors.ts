import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {join,resolve} from 'node:path';import {tmpdir} from 'node:os';import {execFileSync,spawnSync} from 'node:child_process';import {createHash} from 'node:crypto';
const pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f',upstream=resolve(process.argv[2]??''),output=process.argv[3];
if(!process.argv[2]||!output)throw Error('Usage: node tools/generate-path-vectors.ts CLEAN_UPSTREAM OUTPUT');
const git=(...args)=>execFileSync('git',['-C',upstream,...args],{env:{...process.env,GIT_NO_REPLACE_OBJECTS:'1'}});
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const goVersion=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!goVersion.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const rows=[],add=(id,path,directory)=>rows.push({id,path:Buffer.from(path).toString('base64'),directory:Buffer.from(directory).toString('base64')});
const paths=['','a.ts','a/../b/','./','../..','a/../../b','a//./b/..','/','//server','//server/share/../','C:','C:/','C:abc','C:\\a\\..\\b','\\\\server\\share\\x','file://','file:///c:','file:///c%3A/../x','file://localhoſt/c:','FILE://LOCALHOST/c:/a/..','http://host','http://host/a/../../b/','http://host/a?x','https://host/#x','^/','^/~ts-uri~/scheme/auth','^/~ts-uri~/scheme/auth/a/../b','a#b','a?b','./http://host/a','/a\\b//c/','a/\u0000/b','^/~ts-uri~/./a/','^/~ts-uri~/./a/a'];
for(const [i,p] of paths.entries())for(const [j,d] of ['/src','C:/src','file:///c:/src','http://host/src','^/~ts-uri~/s/a/dir','','/a\\'].entries())add(`boundary/${i}/${j}`,p,d);
for(let b=0;b<256;b++)add(`raw/${b}`,Buffer.concat([Buffer.from('a/'),Buffer.from([b]),Buffer.from('/../x')]),'/src');
for(let b=0;b<256;b++)add(`retained/${b}`,Buffer.concat([Buffer.from('a/'),Buffer.from([b]),Buffer.from('/x')]),'/src');
const temp=mkdtempSync(join(tmpdir(),'tscs-path-'));try{
 const input=join(temp,'input.json'),result=join(temp,'output.json'),overlay=join(temp,'overlay.json'),probe=resolve('tools/fixture-harness/path_oracle_test.go');writeFileSync(input,JSON.stringify(rows));writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/tspath/tscs_path_oracle_test.go')]:probe}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsPathOracle$','-count=1','./internal/tspath'],{cwd:join(upstream,'tsc'),encoding:'utf8',env:{...process.env,GOTOOLCHAIN:'local',GOWORK:'off',TSCS_PATH_INPUT:input,TSCS_PATH_OUTPUT:result}});if(run.status!==0)throw Error(run.stdout+run.stderr);
 const vectors=JSON.parse(readFileSync(result,'utf8'));if(vectors.length!==rows.length)throw Error('Lost vectors');const hash=x=>createHash('sha256').update(x).digest('hex');
 writeFileSync(output,JSON.stringify({upstreamCommit:pin,goVersion,probeSha256:hash(readFileSync(probe)),sourceHashes:Object.fromEntries(['path.go','rooted_path.go','dynamic.go'].map(p=>['tsc/internal/tspath/'+p,hash(git('show',`${pin}:tsc/internal/tspath/${p}`))])),vectors},null,2)+'\n');console.log(run.stdout.trim());console.log(`${vectors.length} actual Go path vectors`);
}finally{rmSync(temp,{recursive:true,force:true});}
