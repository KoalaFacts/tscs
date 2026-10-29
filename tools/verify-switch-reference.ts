import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {join,resolve,basename} from 'node:path';

// Independent bounded oracle, NOT full original TS7 harness acceptance.
const [referenceArg,outputArg,optionalSuitePath]=process.argv.slice(2);
assert.ok(referenceArg&&outputArg,'usage: node tools/verify-switch-reference.ts <pinned-go-tsc> <new-evidence-directory>');
const reference=resolve(referenceArg),out=resolve(outputArg),root=process.cwd();
assert.ok(!existsSync(out),'refusing to overwrite source-bound evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const suitePath=optionalSuitePath??'fixtures/switch-flow/semantic-cases.json';
const completion=optionalSuitePath==='fixtures/switch-completion/semantic-cases.json';
const loop=optionalSuitePath==='fixtures/loop-control/semantic-cases.json';
const classic=optionalSuitePath==='fixtures/classic-for/semantic-cases.json';
const numeric=optionalSuitePath==='fixtures/numeric-mutation/semantic-cases.json';
const throwFlow=optionalSuitePath==='fixtures/throw-flow/semantic-cases.json';
const tryCatch=optionalSuitePath==='fixtures/try-catch/semantic-cases.json';
const voidReturn=optionalSuitePath==='fixtures/void-return/semantic-cases.json';
const optionalParameters=optionalSuitePath==='fixtures/optional-parameters/semantic-cases.json';
assert.ok(optionalSuitePath==null||completion||loop||classic||numeric||throwFlow||tryCatch||voidReturn||optionalParameters,'unknown frozen bounded reference cohort');
const suite=JSON.parse(readFileSync(suitePath,'utf8'));
assert.equal(suite.kind,optionalParameters?'BOUNDED_OPTIONAL_PARAMETERS_NOT_ORIGINAL_TS7_ACCEPTANCE':voidReturn?'BOUNDED_VOID_RETURN_NOT_ORIGINAL_TS7_ACCEPTANCE':tryCatch?'BOUNDED_TRY_CATCH_NOT_ORIGINAL_TS7_ACCEPTANCE':throwFlow?'BOUNDED_THROW_FLOW_NOT_ORIGINAL_TS7_ACCEPTANCE':numeric?'BOUNDED_NUMERIC_MUTATION_DO_WHILE_NOT_ORIGINAL_TS7_ACCEPTANCE':classic?'BOUNDED_CLASSIC_FOR_NOT_ORIGINAL_TS7_ACCEPTANCE':loop
  ?'BOUNDED_WHILE_CONTINUE_BREAK_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :completion?'BOUNDED_SWITCH_COMPLETION_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :'BOUNDED_DISCRIMINANT_SWITCH_RETURN_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length,optionalParameters?12:voidReturn?12:tryCatch?14:throwFlow?22:completion?12:numeric?37:14,'do not shrink the registered cohort');
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
// Loop cases exercise number[] indexing, length and for..of element types.
// The frozen bootstrap profile has an intentionally empty Array<T>; reuse
// the existing bounded array profile symmetrically for both compilers.
// Keep the original switch profiles and every case/expectation unchanged.
const minimalProfile=(loop||classic||numeric)?'fixtures/array-iteration/minimal.d.ts':'fixtures/minimal.d.ts';
const minimal=readFileSync(minimalProfile,'utf8');
// An explicitly registered case may choose the already-checked readonly String
// ambient profile. Both compilers receive identical file bytes for that case.
const allowedProfiles=new Set([minimalProfile,'fixtures/string-primitives/minimal.d.ts']);
const hash=(data:string|Buffer)=>createHash('sha256').update(data).digest('hex');
const manifest:{path:string;length:number;sha256:string}[]=[];
function save(name:string,contents:string) {
  const bytes=Buffer.from(contents);
  const path=join(out,name);
  writeFileSync(path,bytes,{flag:'wx',flush:true});
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
  assert.match(item.id,optionalParameters?/^optional-parameters:(positive|negative)-\d{2}$/:voidReturn?/^void-return:(positive|negative)-\d{2}$/:tryCatch?/^try-catch:(positive|negative)-\d{2}$/:throwFlow?/^throw-flow:(positive|negative)-\d{2}$/:numeric?/^numeric-mutation:(positive|negative)-\d+$/:classic?/^classic-for:(positive|negative)-\d+$/:loop?/^loop-control:(positive|negative)-\d+$/:completion?/^switch-completion:(positive|negative)-\d+$/:/^switch-flow:(positive|negative)-\d+$/);
  assert.ok(!ids.has(item.id),'duplicate stable case ID');ids.add(item.id);
  assert.equal(typeof item.source,'string');
  assert.ok(Array.isArray(item.expectedCodes));
  const folder=join(out,'case-'+String(i).padStart(3,'0'));mkdirSync(folder);
  save('case-'+String(i).padStart(3,'0')+'/input.ts',item.source);
  const caseProfile=item.minimalProfile??minimalProfile;
  assert.ok(allowedProfiles.has(caseProfile),'unknown registered minimal declaration profile');
  const caseMinimal=caseProfile===minimalProfile?minimal:readFileSync(caseProfile,'utf8');
  save('case-'+String(i).padStart(3,'0')+'/minimal.d.ts',caseMinimal);
  const args=['--noEmit','--noLib','--target','es2015','--pretty','false','input.ts','minimal.d.ts'];
  const expected=run(reference,args,folder);
  const actual=run(process.env.DOTNET??'dotnet',[product,...args],folder);
  save('case-'+i+'-reference.json',JSON.stringify(expected,null,2));
  save('case-'+i+'-product.json',JSON.stringify(actual,null,2));
  const codes=[...item.expectedCodes].sort(),refCodes=diagnosticCodes(expected),actualCodes=diagnosticCodes(actual);
  const exit=codes.length?2:0;
  const match=expected.exit===exit && actual.exit===exit &&
    JSON.stringify(codes)===JSON.stringify(refCodes) &&
    JSON.stringify(codes)===JSON.stringify(actualCodes);
  const row={id:item.id,inputSha256:hash(item.source),
    minimalProfile:caseProfile,minimalProfileSha256:hash(caseMinimal),
    expectedCodes:codes,referenceCodes:refCodes,
    productCodes:actualCodes,referenceExit:expected.exit,productExit:actual.exit,
    status:match?'MATCH_EXIT_AND_CODES':'MISMATCH',originalAcceptanceCredit:false};
  results.push(row);
  if(!match)mismatches.push(row);
}
save('results.json',JSON.stringify(results,null,2));
save('summary.json',JSON.stringify({sourceSha,upstreamCommit:pin.commit,referenceVersion:version.stdout.trim(),
  referenceSha256:hash(readFileSync(reference)),productCliSha256:hash(readFileSync(product)),
  suiteSha256:hash(readFileSync(suitePath)),scriptSha256:hash(readFileSync(import.meta.filename)),
  minimalProfile,minimalProfileSha256:hash(minimal),
  registeredProfiles:[...new Set(results.map(row=>(row as {minimalProfile:string}).minimalProfile))].sort(),
  matches:results.length-mismatches.length,mismatches:mismatches.length,originalAcceptanceCredit:0,
  fullOriginalScope:'UNEXPANDED',comparison:'bounded code arrays and exits, not original harness',
  referenceExecutable:basename(reference)},null,2));
for(const row of manifest){const bytes=readFileSync(join(out,row.path));
  assert.equal(bytes.length,row.length);assert.equal(hash(bytes),row.sha256);}
writeFileSync(join(out,'manifest.json'),JSON.stringify({files:manifest},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatches,[],'genuine pinned-Go mismatches; raw outputs preserved');
console.log(String(results.length)+' bounded '+(optionalParameters?'optional-parameters':voidReturn?'void-return':tryCatch?'try-catch':throwFlow?'throw-flow':numeric?'numeric-mutation':classic?'classic-for':loop?'loop-control':completion?'switch-completion':'switch-flow')+' sources matched pinned Go codes/exits; no original TS7 acceptance.');
