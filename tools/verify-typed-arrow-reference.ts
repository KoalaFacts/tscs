import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {join,resolve,basename} from 'node:path';

// Fixed pinned TypeScript Go reference, independently from original TS7 acceptance.
const [referenceArg,outputArg]=process.argv.slice(2);
assert.ok(referenceArg&&outputArg,'usage: node tools/verify-typed-arrow-reference.ts <pinned-go-tsc> <new-evidence-directory>');
const reference=resolve(referenceArg),out=resolve(outputArg),root=process.cwd();
assert.ok(!existsSync(out),'refusing to overwrite source-bound run evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const suitePath='fixtures/typed-arrow/semantic-cases.json';
const suite=JSON.parse(readFileSync(suitePath,'utf8'));
assert.equal(suite.kind,'BOUNDED_TYPED_ARROW_EXPRESSIONS_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length,48,'do not shrink frozen or additive reference cases');
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const profilePath='fixtures/minimal.d.ts',profile=readFileSync(profilePath,'utf8');
const hash=(v:string|Buffer)=>createHash('sha256').update(v).digest('hex');
const manifest:{path:string;length:number;sha256:string}[]=[];
function save(path:string,content:string) {
  const bytes=Buffer.from(content);
  writeFileSync(join(out,path),bytes,{flag:'wx',flush:true});
  manifest.push({path,length:bytes.length,sha256:hash(bytes)});
}
function run(exe:string,args:string[],cwd:string) {
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
assert.ok(version.stdout.includes(pin.version),'wrong pinned Go reference');
save('version.json',JSON.stringify(version,null,2));
const sourceSha=run('git',['rev-parse','HEAD'],root).stdout.trim();
const codes=(r:ReturnType<typeof run>)=>
  [...(r.stdout+'\n'+r.stderr).matchAll(/error (TS\d+):/g)].map(x=>x[1]).sort();
const ids=new Set<string>(),rows:object[]=[],mismatch:object[]=[];
for(let i=0;i<suite.cases.length;i++) {
  const item=suite.cases[i];
  assert.match(item.id,/^typed-arrow:(positive|negative)-\d{2}$/);
  assert.ok(!ids.has(item.id),'duplicate case ID');ids.add(item.id);
  assert.equal(typeof item.source,'string');
  assert.ok(Array.isArray(item.expectedCodes));
  const dir='case-'+String(i).padStart(3,'0');
  mkdirSync(join(out,dir));
  save(dir+'/input.ts',item.source);
  save(dir+'/minimal.d.ts',profile);
  const args=['--noEmit','--noLib','--target','es2015','--pretty','false','input.ts','minimal.d.ts'];
  const expected=run(reference,args,join(out,dir));
  const actual=run(process.env.DOTNET??'dotnet',[product,...args],join(out,dir));
  save('reference-'+i+'.json',JSON.stringify(expected,null,2));
  save('product-'+i+'.json',JSON.stringify(actual,null,2));
  const expectedCodes=[...item.expectedCodes].sort();
  const refCodes=codes(expected),productCodes=codes(actual),exit=expectedCodes.length?2:0;
  const match=expected.exit===exit&&actual.exit===exit&&
    JSON.stringify(expectedCodes)===JSON.stringify(refCodes)&&
    JSON.stringify(expectedCodes)===JSON.stringify(productCodes);
  const row={id:item.id,sourceSha256:hash(item.source),expectedCodes,refCodes,productCodes,
    referenceExit:expected.exit,productExit:actual.exit,
    status:match?'MATCH_EXIT_AND_CODES':'MISMATCH',originalAcceptanceCredit:false};
  rows.push(row);
  if(!match)mismatch.push(row);
}
save('results.json',JSON.stringify(rows,null,2));
save('summary.json',JSON.stringify({
  sourceSha,upstreamCommit:pin.commit,referenceVersion:version.stdout.trim(),
  referenceSha256:hash(readFileSync(reference)),productSha256:hash(readFileSync(product)),
  suitePath,suiteSha256:hash(readFileSync(suitePath)),scriptSha256:hash(readFileSync(import.meta.filename)),
  profilePath,profileSha256:hash(profile),matches:rows.length-mismatch.length,mismatches:mismatch.length,
  originalAcceptanceCredit:0,fullOriginalScope:'UNEXPANDED',referenceExecutable:basename(reference)
},null,2));
for(const file of manifest) {
  const bytes=readFileSync(join(out,file.path));
  assert.equal(bytes.length,file.length);
  assert.equal(hash(bytes),file.sha256);
}
writeFileSync(join(out,'manifest.json'),JSON.stringify({files:manifest},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatch,[],'genuine pinned Go mismatch; raw per-case results preserved');
console.log('typed expression arrows: '+rows.length+' stable source/code/exit cases matched pinned Go; original TS7 credit=0.');
