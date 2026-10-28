import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {join,resolve,basename} from 'node:path';

// Bounded semantic profile, NOT the original TS7 compiler harness or acceptance.
// Every source is independently compiled on each side. Keep raw mismatches.
const [referenceArgument,outputArgument]=process.argv.slice(2);
assert.ok(referenceArgument && outputArgument,
  'usage: node tools/verify-literal-types-reference.ts <pinned-go-tsc> <new-output-directory>');
const root=process.cwd();
const reference=resolve(referenceArgument),out=resolve(outputArgument);
assert.ok(!existsSync(out),'refusing to overwrite bounded differential evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const suiteFile='fixtures/literal-types/semantic-cases.json';
const suite=JSON.parse(readFileSync(suiteFile,'utf8'));
assert.equal(suite.kind,'BOUNDED_LITERAL_TYPES_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length,18,'do not shrink the frozen literal type cohort');
const minimal=readFileSync('fixtures/minimal.d.ts','utf8');
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const compiler=resolve('src/Tscs.Compiler/bin/Release/net10.0/Tscs.Compiler.dll');
const sha=(data:string|Buffer)=>createHash('sha256').update(data).digest('hex');
const artifacts:{path:string;length:number;sha256:string}[]=[];
function save(file:string,content:string){
  const data=Buffer.from(content);
  writeFileSync(join(out,file),data,{flag:'wx',flush:true});
  artifacts.push({path:file,length:data.length,sha256:sha(data)});
}
function run(exe:string,args:string[],cwd:string) {
  const result=spawnSync(exe,args,{cwd,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
    env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1',
      GOPROXY:'off',GOSUMDB:'off',GOTOOLCHAIN:'local',GOWORK:'off'}});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler must not crash or time out');
  assert.notEqual(result.status,null);
  return {command:[exe,...args],exit:result.status,stdout:result.stdout,stderr:result.stderr};
}
const version=run(reference,['--version'],root);
assert.equal(version.exit,0);
assert.ok(version.stdout.includes(pin.version),'reference does not match the frozen TS7 version');
save('reference-version.json',JSON.stringify(version,null,2));
const sourceSha=run('git',['rev-parse','HEAD'],root).stdout.trim();
const diagnosticCodes=(result:ReturnType<typeof run>)=>
  [...(result.stdout+'\n'+result.stderr).matchAll(/error (TS\d+):/g)].map(match=>match[1]).sort();
const ids=new Set<string>(),results:object[]=[],mismatches:object[]=[];
for(let i=0;i<suite.cases.length;i++){
  const entry=suite.cases[i];
  assert.match(entry.id,/^literal-types:(positive|negative)-\d+$/);
  assert.ok(!ids.has(entry.id));ids.add(entry.id);
  assert.equal(typeof entry.source,'string');
  assert.ok(Array.isArray(entry.expectedCodes));
  const folder=join(out,'case-'+String(i).padStart(3,'0'));
  mkdirSync(folder);
  save(`case-${String(i).padStart(3,'0')}/input.ts`,entry.source);
  save(`case-${String(i).padStart(3,'0')}/minimal.d.ts`,minimal);
  const args=['--noEmit','--noLib','--target','es2015','--pretty','false','input.ts','minimal.d.ts'];
  const expected=run(reference,args,folder);
  const actual=run(process.env.DOTNET??'dotnet',[product,...args],folder);
  save(`case-${i}-reference.json`,JSON.stringify(expected,null,2));
  save(`case-${i}-product.json`,JSON.stringify(actual,null,2));
  const required=[...entry.expectedCodes].sort();
  const referenceCodes=diagnosticCodes(expected),productCodes=diagnosticCodes(actual);
  const expectedExit=required.length?2:0;
  const matched=expected.exit===expectedExit && actual.exit===expectedExit &&
    JSON.stringify(referenceCodes)===JSON.stringify(required) &&
    JSON.stringify(productCodes)===JSON.stringify(required);
  const row={id:entry.id,inputSha256:sha(entry.source),referenceExit:expected.exit,productExit:actual.exit,
    referenceCodes,productCodes,expectedCodes:required,
    status:matched?'MATCH_EXIT_AND_DIAGNOSTIC_CODES':'MISMATCH',originalAcceptanceCredit:false};
  results.push(row);
  if(!matched)mismatches.push(row);
}
save('results.json',JSON.stringify(results,null,2));
save('summary.json',JSON.stringify({kind:suite.kind,sourceSha,upstreamCommit:pin.commit,
  referenceVersion:version.stdout.trim(),referenceSha256:sha(readFileSync(reference)),
  productCliSha256:sha(readFileSync(product)),productCompilerSha256:sha(readFileSync(compiler)),
  suiteSha256:sha(readFileSync(suiteFile)),minimalLibSha256:sha(readFileSync('fixtures/minimal.d.ts')),
  scriptSha256:sha(readFileSync(import.meta.filename)),
  matches:results.length-mismatches.length,mismatches:mismatches.length,
  comparison:'exit and diagnostic code arrays only; NOT full original TS7 harness assertions',
  originalAcceptanceCredit:0,releaseEligible:false,referenceExecutable:basename(reference)},null,2));
for(const entry of artifacts){const data=readFileSync(join(out,entry.path));
  assert.equal(data.length,entry.length);assert.equal(sha(data),entry.sha256);}
writeFileSync(join(out,'manifest.json'),JSON.stringify({artifacts},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatches,[],'literal type differential mismatches; raw evidence retained');
console.log(`${results.length} bounded primitive literal codes and exits matched pinned ${pin.commit}; no original upstream acceptance granted.`);
