import assert from "node:assert/strict";
import {createHash} from "node:crypto";
import {spawnSync} from "node:child_process";
import {existsSync,mkdirSync,readFileSync,writeFileSync} from "node:fs";
import {resolve,join,basename} from "node:path";

// A bounded semantic differential, not original upstream runner acceptance.
// The exact same source is compiled by pinned Go and the C# product, with
// raw commands and diagnostic output preserved for every success and failure.
const [referenceArgument,outputArgument]=process.argv.slice(2);
assert.ok(referenceArgument && outputArgument,
  "usage: node tools/verify-array-iteration-reference.ts <pinned-go-tsc> <fresh-output-folder>");
const pin=JSON.parse(readFileSync("upstream.lock.json","utf8"));
const suitePath="fixtures/array-iteration/semantic-cases.json";
const suite=JSON.parse(readFileSync(suitePath,"utf8"));
assert.equal(suite.kind,"BOUNDED_ARRAY_INDEX_FOROF_NOT_ORIGINAL_TS7_ACCEPTANCE");
assert.equal(suite.cases.length,14,"frozen bounded array cohort must not shrink");
const reference=resolve(referenceArgument);
const out=resolve(outputArgument);
assert.ok(!existsSync(out),"refusing to overwrite any existing evidence");
mkdirSync(out,{recursive:true});
const hash=(bytes:string|Buffer)=>createHash("sha256").update(bytes).digest("hex");
const artifacts:{path:string;sha256:string;length:number}[]=[];
function save(file:string,content:string){
  const bytes=Buffer.from(content);
  writeFileSync(join(out,file),bytes,{flag:"wx",flush:true});
  artifacts.push({path:file,sha256:hash(bytes),length:bytes.length});
}
function run(exe:string,args:string[],cwd:string){
  const res=spawnSync(exe,args,{cwd,encoding:"utf8",timeout:30_000,maxBuffer:4*1024*1024,
    env:{...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:"1",TESTINGPLATFORM_TELEMETRY_OPTOUT:"1",
      GOPROXY:"off",GOSUMDB:"off",GOTOOLCHAIN:"local",GOWORK:"off"}});
  assert.equal(res.error,undefined,String(res.error));
  assert.equal(res.signal,null);
  assert.notEqual(res.status,null);
  return {command:[exe,...args],status:res.status,stdout:res.stdout,stderr:res.stderr};
}
const cwd=process.cwd();
const version=run(reference,["--version"],cwd);
assert.equal(version.status,0);
assert.ok(version.stdout.includes(pin.version),"pinned reference version mismatch");
save("reference-version.json",JSON.stringify(version,null,2));
const sourceSha=run("git",["rev-parse","HEAD"],cwd).stdout.trim();
const product=resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll");
const compiler=resolve("src/Tscs.Compiler/bin/Release/net10.0/Tscs.Compiler.dll");
const minimal=readFileSync("fixtures/minimal.d.ts","utf8");
const codes=(r:ReturnType<typeof run>)=>
  [...(r.stdout+"\n"+r.stderr).matchAll(/error (TS\d+):/g)].map(m=>m[1]).sort();
const seen=new Set<string>();
const results:object[]=[];
const mismatches:object[]=[];
for(let i=0;i<suite.cases.length;i++){
  const item=suite.cases[i];
  assert.match(item.id,/^array-iteration:(positive|negative)-\d+$/);
  assert.ok(!seen.has(item.id),"duplicate test identity");seen.add(item.id);
  assert.ok(Array.isArray(item.expectedCodes));
  const folder=join(out,`case-${String(i).padStart(3,"0")}`);mkdirSync(folder);
  save(`case-${String(i).padStart(3,"0")}/input.ts`,item.source);
  save(`case-${String(i).padStart(3,"0")}/minimal.d.ts`,minimal);
  const args=["--noEmit","--noLib","--target","es2015","--pretty","false","input.ts","minimal.d.ts"];
  const ref=run(reference,args,folder);
  const actual=run(process.env.DOTNET??"dotnet",[product,...args],folder);
  // Evidence includes complete raw stdout/stderr and the executable arguments.
  save(`case-${i}-reference.json`,JSON.stringify(ref,null,2));
  save(`case-${i}-product.json`,JSON.stringify(actual,null,2));
  const expectedCodes=[...item.expectedCodes].sort();
  const expectedExit=expectedCodes.length?2:0;
  const good=ref.status===expectedExit && actual.status===expectedExit &&
    JSON.stringify(codes(ref))===JSON.stringify(expectedCodes) &&
    JSON.stringify(codes(actual))===JSON.stringify(expectedCodes);
  const row={id:item.id,inputSha256:hash(item.source),referenceCodes:codes(ref),productCodes:codes(actual),
    referenceExit:ref.status,productExit:actual.status,expectedCodes,
    result:good?"MATCH_EXIT_AND_DIAGNOSTIC_CODES":"MISMATCH",originalAcceptanceCredit:false};
  results.push(row);
  if(!good)mismatches.push(row);
}
save("results.json",JSON.stringify(results,null,2));
save("summary.json",JSON.stringify({kind:suite.kind,sourceSha,upstreamCommit:pin.commit,
  referenceVersion:version.stdout.trim(),referenceBinarySha256:hash(readFileSync(reference)),
  productCliSha256:hash(readFileSync(product)),productCompilerSha256:hash(readFileSync(compiler)),
  cohortSha256:hash(readFileSync(suitePath)),scriptSha256:hash(readFileSync(import.meta.filename)),
  matches:results.length-mismatches.length,mismatches:mismatches.length,
  comparison:"exit and code arrays only; NOT original TS7 assertions or full diagnostic parity",
  referenceExecutable:basename(reference),originalAcceptanceCredit:0,releaseEligible:false},null,2));
for(const file of artifacts){const bytes=readFileSync(join(out,file.path));
  assert.equal(bytes.length,file.length);assert.equal(hash(bytes),file.sha256);}
writeFileSync(join(out,"manifest.json"),JSON.stringify({artifacts},null,2),{flag:"wx",flush:true});
assert.deepEqual(mismatches,[],"bounded array differential mismatches (raw evidence retained)");
console.log(`${results.length} bounded array/index/for..of semantics matched pinned ${pin.commit}; original TS7 acceptance unchanged.`);
