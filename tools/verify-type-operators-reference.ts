import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {join,resolve,basename} from 'node:path';

// Fixed upstream-Go diagnostics/exit comparison, NOT original TS7 harness acceptance.
const [referenceArg,outputArg]=process.argv.slice(2);
assert.ok(referenceArg&&outputArg,'usage: node tools/verify-type-operators-reference.ts <pinned-go-tsc> <new-evidence-directory>');
const reference=resolve(referenceArg),out=resolve(outputArg),root=process.cwd();
assert.ok(!existsSync(out),'refusing to overwrite source-bound evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const suitePath='fixtures/type-operators/semantic-cases.json';
const suite=JSON.parse(readFileSync(suitePath,'utf8'));
assert.equal(suite.kind,'BOUNDED_TYPE_ASSERTIONS_SATISFIES_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length,25,'do not shrink the frozen type-operator cohort');
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const minimalProfile='fixtures/array-iteration/minimal.d.ts';
const minimal=readFileSync(minimalProfile,'utf8');
const hash=(data:string|Buffer)=>createHash('sha256').update(data).digest('hex');
const manifest:{path:string;length:number;sha256:string}[]=[];
function save(name:string,contents:string) {
  const bytes=Buffer.from(contents);
  writeFileSync(join(out,name),bytes,{flag:'wx',flush:true});
  manifest.push({path:name,length:bytes.length,sha256:hash(bytes)});
}
function run(exe:string,args:string[],cwd:string){
  const result=spawnSync(exe,args,{cwd,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
    env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1',
      GOPROXY:'off',GOSUMDB:'off',GOTOOLCHAIN:'local',GOWORK:'off'}});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  assert.notEqual(result.status,null);
  return {command:[exe,...args],exit:result.status,stdout:result.stdout,stderr:result.stderr};
}
const version=run(reference,['--version'],root);
assert.equal(version.exit,0);
assert.ok(version.stdout.includes(pin.version),'wrong pinned Go compiler');
save('reference-version.json',JSON.stringify(version,null,2));
const sourceSha=run('git',['rev-parse','HEAD'],root).stdout.trim();
const diagnosticCodes=(r:ReturnType<typeof run>)=>
  [...(r.stdout+'\n'+r.stderr).matchAll(/error (TS\d+):/g)].map(x=>x[1]).sort();
const ids=new Set<string>(),results:object[]=[],mismatches:object[]=[];
for(let i=0;i<suite.cases.length;i++){
  const item=suite.cases[i];
  assert.match(item.id,/^type-operators:(positive|negative)-\d{2}$/);
  assert.ok(!ids.has(item.id),'duplicate stable case ID');ids.add(item.id);
  assert.equal(typeof item.source,'string');
  assert.ok(Array.isArray(item.expectedCodes));
  const key='case-'+String(i).padStart(3,'0');
  const folder=join(out,key);mkdirSync(folder);
  save(key+'/input.ts',item.source);
  save(key+'/minimal.d.ts',minimal);
  const args=['--noEmit','--noLib','--target','es2015','--pretty','false','input.ts','minimal.d.ts'];
  const expected=run(reference,args,folder);
  const actual=run(process.env.DOTNET??'dotnet',[product,...args],folder);
  save(key+'-reference.json',JSON.stringify(expected,null,2));
  save(key+'-product.json',JSON.stringify(actual,null,2));
  const codes=[...item.expectedCodes].sort(),refCodes=diagnosticCodes(expected),actualCodes=diagnosticCodes(actual);
  const exit=codes.length?2:0;
  const match=expected.exit===exit && actual.exit===exit &&
    JSON.stringify(codes)===JSON.stringify(refCodes) &&
    JSON.stringify(codes)===JSON.stringify(actualCodes);
  const row={id:item.id,inputSha256:hash(item.source),expectedCodes:codes,referenceCodes:refCodes,
    productCodes:actualCodes,referenceExit:expected.exit,productExit:actual.exit,
    status:match?'MATCH_EXIT_AND_CODES':'MISMATCH',originalAcceptanceCredit:false};
  results.push(row);if(!match)mismatches.push(row);
}
save('results.json',JSON.stringify(results,null,2));
save('summary.json',JSON.stringify({sourceSha,upstreamCommit:pin.commit,referenceVersion:version.stdout.trim(),
  referenceSha256:hash(readFileSync(reference)),productCliSha256:hash(readFileSync(product)),
  suitePath,suiteSha256:hash(readFileSync(suitePath)),scriptSha256:hash(readFileSync(import.meta.filename)),
  minimalProfile,minimalProfileSha256:hash(minimal),matches:results.length-mismatches.length,
  mismatches:mismatches.length,originalAcceptanceCredit:0,fullOriginalScope:'UNEXPANDED',
  comparison:'bounded code arrays and exits, not original harness',referenceExecutable:basename(reference)},null,2));
for(const item of manifest){
  const bytes=readFileSync(join(out,item.path));
  assert.equal(bytes.length,item.length);assert.equal(hash(bytes),item.sha256);
}
writeFileSync(join(out,'manifest.json'),JSON.stringify({files:manifest},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatches,[],'real pinned-Go mismatches; raw output retained');
console.log(String(results.length)+' bounded as/satisfies cases matched pinned Go codes/exits; no original TS7 acceptance.');
