import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {basename,join,resolve} from 'node:path';

// An isolated, source-bound differential. Not original TS7 harness acceptance.
const [referenceArg,outputArg,optionalSuitePath]=process.argv.slice(2);
assert.ok(referenceArg&&outputArg,'usage: node tools/verify-union-writes-reference.ts <pinned-go-tsc> <fresh-evidence-directory>');
const root=process.cwd(),reference=resolve(referenceArg),out=resolve(outputArg);
assert.ok(!existsSync(out),'never overwrite prior evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const calls=optionalSuitePath==='fixtures/union-callable/semantic-cases.json';
assert.ok(optionalSuitePath==null||calls,'unregistered bounded cohort');
const suitePath=optionalSuitePath??'fixtures/union-property-writes/semantic-cases.json';
const suite=JSON.parse(readFileSync(suitePath,'utf8'));
assert.equal(suite.kind,calls?'BOUNDED_COMMON_UNION_CALLS_NOT_ORIGINAL_TS7_ACCEPTANCE':
  'BOUNDED_COMMON_UNION_PROPERTY_WRITES_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.upstreamCommit,pin.commit);
assert.equal(suite.cases.length,16,'do not shrink or silently reclassify either frozen cohort');
const minimalProfile='fixtures/minimal.d.ts';
const minimal=readFileSync(minimalProfile);
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const sha256=(v:string|Buffer)=>createHash('sha256').update(v).digest('hex');
const manifest:{path:string;length:number;sha256:string}[]=[];
function save(path:string,data:string|Buffer){
  const bytes=Buffer.isBuffer(data)?data:Buffer.from(data);
  writeFileSync(join(out,path),bytes,{flag:'wx',flush:true});
  manifest.push({path,length:bytes.length,sha256:sha256(bytes)});
}
function run(executable:string,args:string[],cwd:string){
  const result=spawnSync(executable,args,{cwd,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
    env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1',
      GOPROXY:'off',GOSUMDB:'off',GOTOOLCHAIN:'local',GOWORK:'off'}});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  assert.notEqual(result.status,null);
  return {command:[executable,...args],exit:result.status,stdout:result.stdout,stderr:result.stderr};
}
const version=run(reference,['--version'],root);
assert.equal(version.exit,0);
assert.ok(version.stdout.includes(pin.version),'reference is not fixed TS7');
save('reference-version.json',JSON.stringify(version,null,2));
const sourceSha=run('git',['rev-parse','HEAD'],root).stdout.trim();
const diagnostics=(value:ReturnType<typeof run>)=>
  [...(value.stdout+'\n'+value.stderr).matchAll(/error (TS\d+):/g)].map(x=>x[1]).sort();
const seen=new Set<string>(),results:object[]=[],mismatches:object[]=[];
for(let index=0;index<suite.cases.length;index++){
  const test=suite.cases[index];
  assert.match(test.id,calls?/^union-callable:(positive|negative)-\d{2}$/:/^union-property-writes:(positive|negative)-\d{2}$/);
  assert.ok(!seen.has(test.id),'duplicate stable case ID');seen.add(test.id);
  assert.equal(typeof test.source,'string');
  assert.ok(Array.isArray(test.expectedCodes));
  const folderName='case-'+String(index).padStart(3,'0');
  const folder=join(out,folderName);mkdirSync(folder);
  save(folderName+'/input.ts',test.source);
  save(folderName+'/minimal.d.ts',minimal);
  const args=['--noEmit','--noLib','--target','es2015','--pretty','false','input.ts','minimal.d.ts'];
  const expected=run(reference,args,folder);
  const actual=run(process.env.DOTNET??'dotnet',[product,...args],folder);
  save(folderName+'/reference.json',JSON.stringify(expected,null,2));
  save(folderName+'/product.json',JSON.stringify(actual,null,2));
  const codes=[...test.expectedCodes].sort();
  const referenceCodes=diagnostics(expected),productCodes=diagnostics(actual);
  const requiredExit=codes.length?2:0;
  const match=expected.exit===requiredExit && actual.exit===requiredExit &&
    JSON.stringify(codes)===JSON.stringify(referenceCodes) &&
    JSON.stringify(codes)===JSON.stringify(productCodes);
  const record={id:test.id,inputSha256:sha256(test.source),
    minimalProfile,minimalSha256:sha256(minimal),expectedCodes:codes,
    referenceCodes,productCodes,referenceExit:expected.exit,productExit:actual.exit,
    status:match?'MATCH_EXIT_AND_CODES':'MISMATCH',originalAcceptanceCredit:false};
  results.push(record);if(!match)mismatches.push(record);
}
save('results.json',JSON.stringify(results,null,2));
save('summary.json',JSON.stringify({sourceSha,upstreamCommit:pin.commit,
  referenceVersion:version.stdout.trim(),referenceSha256:sha256(readFileSync(reference)),
  productSha256:sha256(readFileSync(product)),suiteSha256:sha256(readFileSync(suitePath)),
  minimalProfile,minimalSha256:sha256(minimal),
  scriptSha256:sha256(readFileSync(import.meta.filename)),
  matches:results.length-mismatches.length,mismatches:mismatches.length,
  originalAcceptanceCredit:0,fullOriginalScope:'UNEXPANDED',
  referenceExecutable:basename(reference)},null,2));
for(const item of manifest){const bytes=readFileSync(join(out,item.path));
  assert.equal(bytes.length,item.length);assert.equal(sha256(bytes),item.sha256);}
writeFileSync(join(out,'manifest.json'),JSON.stringify({files:manifest},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatches,[],'pinned-Go mismatches; raw evidence was retained');
console.log(results.length+' finite union-property writes matched pinned Go; no original acceptance credit.');
