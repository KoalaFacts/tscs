import {readFileSync,writeFileSync,mkdtempSync,rmSync} from 'node:fs';
import {resolve,join} from 'node:path';import {tmpdir} from 'node:os';import {execFileSync,spawnSync} from 'node:child_process';import {sha256} from './ts7-scope.ts';
const [up,out]=process.argv.slice(2),pin='8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';if(!up||!out)throw Error('Usage: node tools/generate-json-ast.ts PINNED_CHECKOUT OUTPUT');
const upstream=resolve(up),git=(...args)=>execFileSync('git',['-C',upstream,...args]);
if(git('rev-parse','HEAD').toString().trim()!==pin||git('status','--porcelain','--untracked-files=no').length||JSON.parse(readFileSync('upstream.lock.json','utf8')).commit!==pin)throw Error('Exact clean pin required');
const version=execFileSync('go',['version'],{encoding:'utf8'}).trim();if(!version.startsWith('go version go1.27.3 '))throw Error('Pinned Go required');
const sources=['','{}','[]','true','false','null','12','-12','"hello"',"'hello'",'{"x":true,"a":[null,1,-2,"z",],}',"{x:'value'}",'{"a":{},"a":{"b":[]}}',' /* pre */ {"😀":"\\uD800", "s":"\\q"} // end','{1:"n"}','{"x":"\\xZ"}','{"x":"\\09"}','{"files":["a.ts","minimal.d.ts"],"compilerOptions":{"noEmit":true,"noLib":true,"target":"es2015",},}', '{"a":[{"b":false},["x",true]]}',"{'😀': 'v'}",'// hi\n[1,2,3,]','{"a":"\\u{1f600}","b":"\\uD800\\uDC00"}'];
// Decimal-integer boundaries are exercised through the actual parser in all
// supported numeric AST positions, not through a formatting-only mirror.
const numbers=new Set(['0','1','1000000000000000128','19686109595169230000','9'.repeat(400)]);
for(let d=-4n;d<=4n;d++)numbers.add(String(9007199254740992n+d));
for(const e of [19n,20n,21n,22n,100n,308n,309n])for(const d of [-1n,0n,1n])numbers.add(String(10n**e+d));
for(const n of numbers)sources.push(n,'-'+n,'[1,{"n":'+n+'},-'+n+']','{'+n+':true}');
const rows=sources.map((source,i)=>({id:String(i),source}));
const temp=mkdtempSync(join(tmpdir(),'tscs-strings-'));try{
 const input=join(temp,'input.json'),output=join(temp,'output.json'),overlay=join(temp,'overlay.json'),probe=resolve('tools/fixture-harness/json_ast_oracle_test.go');writeFileSync(input,JSON.stringify(rows));writeFileSync(overlay,JSON.stringify({Replace:{[join(upstream,'tsc/internal/parser/tscs_json_ast_test.go')]:probe}}));
 const run=spawnSync('go',['test','-overlay',overlay,'-run','^TestTscsJsonAstOracle$','-count=1','./internal/parser'],{cwd:join(upstream,'tsc'),encoding:'utf8',timeout:180000,env:{...process.env,GOWORK:'off',GOTOOLCHAIN:'local',TSCS_STRING_INPUT:input,TSCS_STRING_OUTPUT:output}});if(process.env.TSCS_STRING_LOG)writeFileSync(process.env.TSCS_STRING_LOG,(run.stdout??'')+(run.stderr??''));if(run.error||run.status!==0)throw Error(run.error??run.stdout+run.stderr);
 const vectors=JSON.parse(readFileSync(output,'utf8'));if(vectors.length!==rows.length||new Set(vectors.map(r=>r.id)).size!==rows.length||vectors.some((r,i)=>r.id!==rows[i].id||r.source!==rows[i].source))throw Error('Input binding mismatch');
 writeFileSync(out,JSON.stringify({upstreamCommit:pin,goVersion:version,jsNumberSourceSha256:sha256(git('show',`${pin}:tsc/internal/jsnum/string.go`)),parserSourceSha256:sha256(git('show',`${pin}:tsc/internal/parser/parser.go`)),scannerSourceSha256:sha256(git('show',`${pin}:tsc/internal/scanner/scanner.go`)),probeSha256:sha256(readFileSync(probe)),tokenFlagsSha256:sha256(git('show',`${pin}:tsc/internal/ast/tokenflags.go`)),diagnosticsSha256:sha256(git('show',`${pin}:tsc/internal/diagnostics/diagnostics_generated.go`)),stringUtilSha256:sha256(git('show',`${pin}:tsc/internal/stringutil/util.go`)),vectors},null,2)+'\n');console.log(JSON.stringify({vectors:vectors.length}));
}finally{rmSync(temp,{recursive:true,force:true});}
