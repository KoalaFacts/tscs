import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';import {tmpdir} from 'node:os';import {execFileSync,spawnSync} from 'node:child_process';import {sha256} from './ts7-scope.ts';
const [up,out]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';if(!up||!out)throw Error('Usage: node tools/generate-unicode-escapes.ts PINNED_CHECKOUT OUTPUT');
const upstream=resolve(up),git=(...args)=>execFileSync('git',['-C',upstream,...args]);
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const version=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!version.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const bodies=[];
for(const n of [0,1,9,10,13,34,39,92,127,128,255])bodies.push('\\x'+n.toString(16).padStart(2,'0'));
for(const n of [0,1,0x7f,0xff,0x100,0x2028,0xd7ff,0xd800,0xdbff,0xdc00,0xdfff,0xe000,0xffff])bodies.push('\\u'+n.toString(16).padStart(4,'0'));
for(const n of [0,0x61,0xffff,0x10000,0x10ffff,0xd800,0xdc00])bodies.push('\\u{'+n.toString(16)+'}');
for(const h of ['\\ud83d','\\u{d83d}'])for(const l of ['\\ude00','\\u{de00}'])bodies.push(h+l);
bodies.push(String.raw`\uD800x\uDC00`,String.raw`\uD800\uD800`,String.raw`\uDC00\uD800`,String.raw`\uD800\n`,String.raw`\x41\u0042\u{43}`,String.raw`\u{0000000000000000000000000000000061}`);
const rows=[];for(const [q,quote] of ["'",'"'].entries())for(const [i,body] of bodies.entries())for(const [p,prefix] of ['a','😀'].entries())rows.push({id:q+'/'+i+'/'+p,literal:quote+prefix+body+'z'+quote});
const temp=mkdtempSync(join(tmpdir(),'tscs-strings-'));try{
 const input=join(temp,'input.json'),output=join(temp,'output.json'),overlay=join(temp,'overlay.json'),probe=resolve('tools/fixture-harness/unicode_escapes_oracle_test.go');writeFileSync(input,JSON.stringify(rows));writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/scanner/tscs_unicode_escapes_test.go')]:probe}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsUnicodeEscapesOracle$','-count=1','./internal/scanner'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',TSCS_STRING_INPUT:input,TSCS_STRING_OUTPUT:output}});if(process.env.TSCS_STRING_LOG)writeFileSync(process.env.TSCS_STRING_LOG,(run.stdout??'')+(run.stderr??''));if(run.error||run.status!==0)throw Error(run.error??run.stdout+run.stderr);
 const vectors=JSON.parse(readFileSync(output,'utf8'));if(vectors.length!==rows.length||new Set(vectors.map(r=>r.id)).size!==rows.length||vectors.some((r,i)=>r.id!==rows[i].id||r.literal!==rows[i].literal))throw Error('Input binding mismatch');
 writeFileSync(out,JSON.stringify({upstreamCommit:pin,goVersion:version,scannerSourceSha256:sha256(git('show',`${pin}:tsc/internal/scanner/scanner.go`)),probeSha256:sha256(readFileSync(probe)),stringUtilSha256:sha256(git('show',`${pin}:tsc/internal/stringutil/util.go`)),vectors},null,2)+'\n');console.log(JSON.stringify({vectors:vectors.length}));
}finally{rmSync(temp,{recursive:true,force:true});}
