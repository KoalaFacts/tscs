import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdirSync,readFileSync,writeFileSync} from 'node:fs';
import {basename,join,resolve} from 'node:path';

// Bounded multi-file source/code/exit oracle, NOT original TS7 acceptance.
const [referenceArg,outputArg]=process.argv.slice(2);
assert.ok(referenceArg && outputArg,'expected pinned Go compiler and fresh output folder');
const root=process.cwd(),reference=resolve(referenceArg),out=resolve(outputArg);
assert.ok(!existsSync(out),'refusing to overwrite reference evidence');
mkdirSync(out,{recursive:true});
const pin=JSON.parse(readFileSync('upstream.lock.json','utf8'));
const suitePath='fixtures/type-reexports/semantic-cases.json';
const suite=JSON.parse(readFileSync(suitePath,'utf8'));
assert.equal(suite.kind,'BOUNDED_TYPE_REEXPORT_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length,12);
const minimal=readFileSync('fixtures/minimal.d.ts','utf8');
const product=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const compiler=resolve('src/Tscs.Compiler/bin/Release/net10.0/Tscs.Compiler.dll');
const sha=(v:string|Buffer)=>createHash('sha256').update(v).digest('hex');
const assets:{path:string;length:number;sha256:string}[]=[];
function save(path:string,value:string) {
  const bytes=Buffer.from(value);
  writeFileSync(join(out,path),bytes,{flag:'wx',flush:true});
  assets.push({path,length:bytes.length,sha256:sha(bytes)});
}
function run(exe:string,args:string[],cwd:string){
  const r=spawnSync(exe,args,{cwd,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,
    env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1',
      GOPROXY:'off',GOSUMDB:'off',GOTOOLCHAIN:'local',GOWORK:'off'}});
  assert.equal(r.error,undefined,String(r.error));
  assert.equal(r.signal,null,'compiler timeout or signal');
  assert.notEqual(r.status,null);
  return {command:[exe,...args],exit:r.status,stdout:r.stdout,stderr:r.stderr};
}
const version=run(reference,['--version'],root);
assert.equal(version.exit,0);
assert.ok(version.stdout.includes(pin.version),'pinned Go version mismatch');
save('reference-version.json',JSON.stringify(version,null,2));
const sourceSha=run('git',['rev-parse','HEAD'],root).stdout.trim();
const codes=(r:ReturnType<typeof run>)=>
  [...(r.stdout+'\n'+r.stderr).matchAll(/error (TS\d+):/g)].map(m=>m[1]).sort();
const ids=new Set<string>(),rows:object[]=[],mismatches:object[]=[];
for(let i=0;i<suite.cases.length;i++){
  const c=suite.cases[i];
  assert.match(c.id,/^type-reexport:(positive|negative)-\d+$/);
  assert.ok(!ids.has(c.id));ids.add(c.id);
  assert.ok(c.files && Object.hasOwn(c.files,'entry.ts'));
  const dirName='case-'+String(i).padStart(3,'0'),cwd=join(out,dirName);
  mkdirSync(cwd);
  const sources:object[]=[];
  for(const [name,source] of Object.entries(c.files)){
    assert.match(name,/^[a-z][a-z0-9-]*\.ts$/);
    assert.equal(typeof source,'string');
    const value=source as string;
    save(dirName+'/'+name,value);
    sources.push({name,length:Buffer.byteLength(value),sha256:sha(value)});
  }
  save(dirName+'/minimal.d.ts',minimal);
  const args=['--noEmit','--noLib','--target','es2015','--module','es2015',
    '--pretty','false','entry.ts','minimal.d.ts'];
  const expected=run(reference,args,cwd);
  const actual=run(process.env.DOTNET??'dotnet',[product,...args],cwd);
  save(dirName+'/reference.json',JSON.stringify(expected,null,2));
  save(dirName+'/product.json',JSON.stringify(actual,null,2));
  const required=[...c.expectedCodes].sort(),referenceCodes=codes(expected),productCodes=codes(actual);
  const wantedExit=required.length?2:0;
  const match=expected.exit===wantedExit && actual.exit===wantedExit &&
    JSON.stringify(required)===JSON.stringify(referenceCodes) &&
    JSON.stringify(required)===JSON.stringify(productCodes);
  const row={id:c.id,sources,expectedCodes:required,referenceCodes,productCodes,
    referenceExit:expected.exit,productExit:actual.exit,
    status:match?'MATCH_EXIT_AND_DIAGNOSTIC_CODES':'MISMATCH',originalAcceptanceCredit:false};
  rows.push(row);if(!match)mismatches.push(row);
}
save('results.json',JSON.stringify(rows,null,2));
save('summary.json',JSON.stringify({kind:suite.kind,sourceSha,upstreamCommit:pin.commit,
  referenceVersion:version.stdout.trim(),referenceSha256:sha(readFileSync(reference)),
  productCliSha256:sha(readFileSync(product)),productCompilerSha256:sha(readFileSync(compiler)),
  suiteSha256:sha(readFileSync(suitePath)),minimalLibSha256:sha(minimal),
  scriptSha256:sha(readFileSync(import.meta.filename)),matches:rows.length-mismatches.length,
  mismatches:mismatches.length,referenceExecutable:basename(reference),
  originalAcceptanceCredit:0,releaseEligible:false},null,2));
for(const item of assets){
  const bytes=readFileSync(join(out,item.path));
  assert.equal(bytes.length,item.length);assert.equal(sha(bytes),item.sha256);
}
writeFileSync(join(out,'manifest.json'),JSON.stringify({assets},null,2),{flag:'wx',flush:true});
assert.deepEqual(mismatches,[],'type reexport mismatches; original results preserved');
console.log('12 bounded type reexport cases compared against pinned Go, no TS7 acceptance credit.');
