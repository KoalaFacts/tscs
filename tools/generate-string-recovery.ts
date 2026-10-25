import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';import {tmpdir} from 'node:os';import {execFileSync,spawnSync} from 'node:child_process';import {sha256} from './ts7-scope.ts';
const [up,out]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';if(!up||!out)throw Error('Usage: node tools/generate-string-recovery.ts PINNED_CHECKOUT OUTPUT');
const upstream=resolve(up),git=(...args)=>execFileSync('git',['-C',upstream,...args]);
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const version=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!version.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const bodies=[String.raw`\x`,String.raw`\x0`,String.raw`\xG`,String.raw`\x0G`,String.raw`\u`,String.raw`\u1`,String.raw`\u12`,String.raw`\u123`,String.raw`\uG`,String.raw`\u12G4`,String.raw`\u{}`,String.raw`\u{G}`,String.raw`\u{61`,String.raw`\u{61G}`,String.raw`\u{110000}`,String.raw`\u{7fffffff}`,String.raw`\u{80000000}`,String.raw`\u{ffffffffffffffffffffffff}`,String.raw`\u{110000`,String.raw`\u{110000G}`,String.raw`\u{000000000000000000000000000110000}`,String.raw`\uD800\u{}`,String.raw`\uD800\u123G`,String.raw`\uD800\u{DC00G}`,String.raw`\uD800\u{DC00}`,String.raw`\u{D800}\uDC00`,String.raw`\xG\uG\u{110000}`,String.raw`\u1234`,String.raw`\x61`,String.raw`\u{10ffff}`,String.raw`\n`,String.raw`\uD800`];
const rows=[];
for(const [q,quote] of ["'",'"'].entries())for(const [i,body] of bodies.entries())for(const [p,prefix] of ['a','😀'].entries()) {
 rows.push({id:`terminated/${q}/${i}/${p}`,source:'let s = '+quote+prefix+body+'z'+quote+'; let after = 1;'});
 rows.push({id:`eof/${q}/${i}/${p}`,source:'let s = '+quote+prefix+body});
}
for(const [q,quote] of ["'",'"'].entries())for(const [i,body] of ['',String.fromCharCode(92),String.raw`\u{110000`].entries())rows.push({id:`end/${q}/${i}`,source:quote+'😀'+body});
for(const [i,line] of ['\r','\n','\r\n'].entries())rows.push({id:`newline/${i}`,source:'"😀\\u0'+line+'next; "\\xZ";'});
for(const r of JSON.parse(readFileSync('tests/Tscs.Compiler.Tests/TestData/string-lines.json','utf8')).vectors)rows.push({id:'plain/'+r.id,source:'let s: string = '+r.literal+';'});
const temp=mkdtempSync(join(tmpdir(),'tscs-strings-'));try{
 const input=join(temp,'input.json'),output=join(temp,'output.json'),overlay=join(temp,'overlay.json'),probe=resolve('tools/fixture-harness/string_recovery_oracle_test.go');writeFileSync(input,JSON.stringify(rows));writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/scanner/tscs_string_recovery_test.go')]:probe}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsStringRecoveryOracle$','-count=1','./internal/scanner'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',TSCS_STRING_INPUT:input,TSCS_STRING_OUTPUT:output}});if(process.env.TSCS_STRING_LOG)writeFileSync(process.env.TSCS_STRING_LOG,(run.stdout??'')+(run.stderr??''));if(run.error||run.status!==0)throw Error(run.error??run.stdout+run.stderr);
 const vectors=JSON.parse(readFileSync(output,'utf8'));if(vectors.length!==rows.length||new Set(vectors.map(r=>r.id)).size!==rows.length||vectors.some((r,i)=>r.id!==rows[i].id||r.source!==rows[i].source))throw Error('Input binding mismatch');
 writeFileSync(out,JSON.stringify({upstreamCommit:pin,goVersion:version,scannerSourceSha256:sha256(git('show',`${pin}:tsc/internal/scanner/scanner.go`)),probeSha256:sha256(readFileSync(probe)),tokenFlagsSha256:sha256(git('show',`${pin}:tsc/internal/ast/tokenflags.go`)),diagnosticsSha256:sha256(git('show',`${pin}:tsc/internal/diagnostics/diagnostics_generated.go`)),stringUtilSha256:sha256(git('show',`${pin}:tsc/internal/stringutil/util.go`)),plainInputsSha256:sha256(readFileSync('tests/Tscs.Compiler.Tests/TestData/string-lines.json')),vectors},null,2)+'\n');console.log(JSON.stringify({vectors:vectors.length}));
}finally{rmSync(temp,{recursive:true,force:true});}
