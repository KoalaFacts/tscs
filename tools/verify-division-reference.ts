import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {join,resolve,basename} from 'node:path';

// Independent bounded oracle, NOT full original TS7 harness acceptance.
const [referenceArg,outputArg,optionalSuitePath]=process.argv.slice(2);
assert.ok(referenceArg&&outputArg,'usage: node tools/verify-division-reference.ts <pinned-go-tsc> <new-evidence-directory> [frozen-suite]');
const reference=resolve(referenceArg),out=resolve(outputArg),root=process.cwd();
assert.ok(!existsSync(out),'refusing to overwrite source-bound evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const suitePath=optionalSuitePath??'fixtures/numeric-division/semantic-cases.json';
const strings=suitePath==='fixtures/string-addition/semantic-cases.json';
const power=suitePath==='fixtures/numeric-exponent/semantic-cases.json';
const stringPrimitives=suitePath==='fixtures/string-primitives/semantic-cases.json';
const stringMethods=suitePath==='fixtures/string-methods/semantic-cases.json';
const stringExtraction=suitePath==='fixtures/string-extraction/semantic-cases.json';
const stringUnicode=suitePath==='fixtures/string-unicode/semantic-cases.json';
const templates=suitePath==='fixtures/no-substitution-template/semantic-cases.json';
const decimalLiterals=suitePath==='fixtures/numeric-literals/semantic-cases.json';
const readonlyArrays=suitePath==='fixtures/readonly-array/semantic-cases.json';
const arrayMethods=suitePath==='fixtures/array-methods/semantic-cases.json';
const arrayInPlace=suitePath==='fixtures/array-in-place/semantic-cases.json';
const arrayQueries=suitePath==='fixtures/array-queries/semantic-cases.json';
const arrayCallbacks=suitePath==='fixtures/array-callbacks/semantic-cases.json';
const arrayFolds=suitePath==='fixtures/array-filter-reduce/semantic-cases.json';
const arrayAccumulator=suitePath==='fixtures/array-accumulator/semantic-cases.json';
const arrayMap=suitePath==='fixtures/array-map/semantic-cases.json';
assert.ok(optionalSuitePath==null||strings||power||stringPrimitives||stringMethods||stringExtraction||stringUnicode||decimalLiterals||templates||readonlyArrays||arrayMethods||arrayInPlace||arrayQueries||arrayCallbacks||arrayFolds||arrayAccumulator||arrayMap,'unknown bounded reference cohort');
const suite=JSON.parse(readFileSync(suitePath,'utf8'));
assert.equal(suite.kind,arrayMap?'BOUNDED_ARRAY_MAP_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :arrayAccumulator?'BOUNDED_ARRAY_ACCUMULATOR_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :arrayFolds?'BOUNDED_ARRAY_FILTER_REDUCE_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :arrayCallbacks?'BOUNDED_ARRAY_CALLBACKS_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :arrayQueries?'BOUNDED_ARRAY_QUERIES_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :arrayMethods?'BOUNDED_ARRAY_MUTATION_METHODS_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :arrayInPlace?'BOUNDED_ARRAY_IN_PLACE_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :readonlyArrays?'BOUNDED_READONLY_ARRAY_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :templates?'BOUNDED_NO_SUBSTITUTION_TEMPLATE_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :decimalLiterals?'BOUNDED_NUMERIC_LITERALS_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :stringUnicode?'BOUNDED_STRING_UNICODE_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :stringExtraction?'BOUNDED_STRING_EXTRACTION_NOT_ORIGINAL_TS7_ACCEPTANCE'
   :stringMethods?'BOUNDED_STRING_METHODS_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :stringPrimitives?'BOUNDED_STRING_PRIMITIVES_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :power?'BOUNDED_NUMERIC_EXPONENTIATION_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :strings?'BOUNDED_STRING_ADDITION_MUTATION_NOT_ORIGINAL_TS7_ACCEPTANCE'
  :'BOUNDED_NUMERIC_DIVISION_REMAINDER_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length,arrayMap?20:arrayAccumulator?16:arrayFolds?20:arrayCallbacks?19:arrayQueries?73:arrayMethods?21:arrayInPlace?26:readonlyArrays?27:templates?37:decimalLiterals?48:stringUnicode?28:stringExtraction?28:stringMethods?24:stringPrimitives?18:power?20:strings?25:24,'do not shrink the registered cohort');
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
// The cohort includes number[] indexing and length. Use the already
// verified array profile for BOTH compilers, not the empty bootstrap Array<T>.
const minimalProfile=arrayAccumulator?'fixtures/array-accumulator/minimal.d.ts':
  arrayMap?'fixtures/array-map/minimal.d.ts':
  arrayFolds?'fixtures/array-filter-reduce/minimal.d.ts':
  arrayCallbacks?'fixtures/array-callbacks/minimal.d.ts':
  arrayQueries?'fixtures/array-queries/minimal.d.ts':
  arrayMethods?'fixtures/array-methods/minimal.d.ts':
  arrayInPlace?'fixtures/array-in-place/minimal.d.ts':
  readonlyArrays?'fixtures/readonly-array/minimal.d.ts':
  stringUnicode?'fixtures/string-unicode/minimal.d.ts':
  stringExtraction?'fixtures/string-extraction/minimal.d.ts':
  stringMethods?'fixtures/string-methods/minimal.d.ts':
  (stringPrimitives||templates)?'fixtures/string-primitives/minimal.d.ts':'fixtures/array-iteration/minimal.d.ts';
const minimal=readFileSync(minimalProfile,'utf8');
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
  assert.match(item.id,arrayAccumulator?/^array-accumulator:(positive|negative)-\d{2}$/
    :arrayMap?/^array-map:(positive|negative)-\d{2}$/
    :arrayFolds?/^array-filter-reduce:(positive|negative)-\d{2}$/
    :arrayCallbacks?/^array-callbacks:(positive|negative)-\d{2}$/
    :arrayQueries?/^array-queries:(positive|negative)-\d{2}$/
    :arrayMethods?/^array-methods:(positive|negative)-\d{2}$/
    :arrayInPlace?/^array-in-place:(positive|negative)-\d{2}$/
    :readonlyArrays?/^readonly-array:(positive|negative)-\d{2}$/
    :stringUnicode?/^string-unicode:(positive|negative)-\d{2}$/
    :stringExtraction?/^string-extraction:(positive|negative)-\d{2}$/
    :templates?/^no-substitution-template:(positive|negative)-\d{2}$/
    :decimalLiterals?/^numeric-literals:(positive|negative)-\d+$/:stringMethods?/^string-methods:(positive|negative)-\d{2}$/:stringPrimitives?/^string-primitives:(positive|negative)-\d+$/:power?/^numeric-exponent:(positive|negative)-\d+$/:strings?/^string-addition:(positive|negative)-\d+$/:/^numeric-division:(positive|negative)-\d+$/);
  assert.ok(!ids.has(item.id),'duplicate stable case ID');ids.add(item.id);
  assert.equal(typeof item.source,'string');
  assert.ok(Array.isArray(item.expectedCodes));
  const folder=join(out,'case-'+String(i).padStart(3,'0'));mkdirSync(folder);
  save('case-'+String(i).padStart(3,'0')+'/input.ts',item.source);
  save('case-'+String(i).padStart(3,'0')+'/minimal.d.ts',minimal);
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
  const row={id:item.id,inputSha256:hash(item.source),expectedCodes:codes,referenceCodes:refCodes,
    productCodes:actualCodes,referenceExit:expected.exit,productExit:actual.exit,
    status:match?'MATCH_EXIT_AND_CODES':'MISMATCH',originalAcceptanceCredit:false};
  results.push(row);
  if(!match)mismatches.push(row);
}
save('results.json',JSON.stringify(results,null,2));
save('summary.json',JSON.stringify({sourceSha,upstreamCommit:pin.commit,referenceVersion:version.stdout.trim(),
  referenceSha256:hash(readFileSync(reference)),productCliSha256:hash(readFileSync(product)),
  suitePath,suiteSha256:hash(readFileSync(suitePath)),scriptSha256:hash(readFileSync(import.meta.filename)),
  minimalProfile,minimalProfileSha256:hash(minimal),
  matches:results.length-mismatches.length,mismatches:mismatches.length,originalAcceptanceCredit:0,
  fullOriginalScope:'UNEXPANDED',comparison:'bounded code arrays and exits, not original harness',
  referenceExecutable:basename(reference)},null,2));
for(const row of manifest){const bytes=readFileSync(join(out,row.path));
  assert.equal(bytes.length,row.length);assert.equal(hash(bytes),row.sha256);}
writeFileSync(join(out,'manifest.json'),JSON.stringify({files:manifest},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatches,[],'genuine pinned-Go mismatches; raw outputs preserved');
console.log(String(results.length)+' bounded '+(arrayAccumulator?'different accumulator typed array reductions':arrayFolds?'readonly/mutable array filtering and folds':arrayCallbacks?'readonly/mutable array predicate callbacks':arrayQueries?'readonly and mutable array query methods' :arrayMethods?'mutable array methods' :arrayInPlace?'native array in-place methods' : readonlyArrays?'readonly array types and mutation' : templates?'bounded interpolated and no-substitution template literals':decimalLiterals?'numeric decimal/exponent/radix literals':stringUnicode?'native Unicode and string formatting':stringExtraction?'native string extraction and replacement':stringMethods?'native string transformations and searches':stringPrimitives?'string primitives':power?'numeric exponentiation':strings?'string addition and mutation':'numeric division and remainder')+' sources matched pinned Go codes/exits; no original TS7 acceptance.');
