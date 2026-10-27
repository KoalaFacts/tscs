import {readFileSync, writeFileSync, mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {isDeepStrictEqual} from 'node:util';

const sourcePath = 'tests/Tscs.Compiler.Tests/CompilationTests.cs';
const registryPath = 'tests/implementation-coverage/registry.json';
const reportPath = 'docs/testing/ts7/implementation-coverage.json';
const allowedMethods = {
  ValidPrimitiveCodeHasNoErrors: 'IMPLEMENTED',
  UnsupportedSyntaxNeverSucceeds: 'UNIMPLEMENTED',
  IntrinsicGlobalThisIsExplicitlyUnsupported: 'UNIMPLEMENTED',
};

function gitBlob(bytes) {
  const prefix = Buffer.from(`blob ${bytes.length}\0`);
  return createHash('sha1').update(prefix).update(bytes).digest('hex');
}

export function extractInlineSources(text, method) {
  const marker = `public void ${method}(`;
  const at = text.indexOf(marker);
  if (at < 0 || text.indexOf(marker, at + marker.length) >= 0) throw Error(`missing or duplicate guard method: ${method}`);
  const start = text.lastIndexOf('[Theory]', at);
  if (start < 0) throw Error(`guard lacks [Theory]: ${method}`);
  const between = text.slice(start + '[Theory]'.length, at);
  if (!/^\s*(?:\[InlineData\([^\r\n]*\)\]\s*)+$/.test(between)) throw Error(`unexpected theory metadata: ${method}`);
  const rows = [...between.matchAll(/\[InlineData\("((?:\\.|[^"\\])*)"(?:\s*,[^\r\n\]]+)?\)\]/g)];
  if (rows.length < 1) throw Error(`empty theory: ${method}`);
  return rows.map(row => {
    try { return JSON.parse(`"${row[1]}"`); }
    catch { throw Error(`unparseable C# string in ${method}`); }
  });
}

export function classifyImplementation(entry, execution = null) {
  if (entry.implementation === 'UNIMPLEMENTED') {
    if (entry.skipReason !== 'UNIMPLEMENTED' || execution !== null) throw Error(`invalid unimplemented skip: ${entry.id}`);
    return {status:'SKIP', reason:'UNIMPLEMENTED', executed:false};
  }
  if (entry.implementation !== 'IMPLEMENTED' || entry.skipReason !== null) throw Error(`invalid implementation state: ${entry.id}`);
  if (execution === null) return {status:'NOT_RUN',reason:null,executed:false};
  if (!['PASS','FAIL','MISMATCH','CRASH','TIMEOUT','BLOCKED'].includes(execution)) throw Error(`unrecognized execution outcome: ${entry.id}`);
  return {status:execution,reason:null,executed:execution!=='BLOCKED'};
}

export function buildCoverage(registry, sourceBytes, upstreamLock) {
  if (registry.schemaVersion!==1 || registry.kind!=='BOUNDED_LOCAL_GUARD_PROBES_NOT_UPSTREAM_ACCEPTANCE') throw Error('invalid coverage registry kind/version');
  if (registry.upstreamCommit!==upstreamLock.commit) throw Error('upstream pin mismatch');
  if (registry.sourcePath!==sourcePath || registry.sourceGitBlob!==gitBlob(sourceBytes)) throw Error('guard source blob mismatch');
  const source = sourceBytes.toString('utf8');
  const seen = new Set();
  const byMethod = Object.fromEntries(Object.keys(allowedMethods).map(method=>[method,[]]));
  for (const item of registry.cases) {
    if (!item || !/^bootstrap:[a-z0-9-]+$/.test(item.id) || seen.has(item.id)) throw Error('duplicate or malformed capability ID');
    seen.add(item.id);
    if (!(item.guardMethod in allowedMethods) || item.implementation!==allowedMethods[item.guardMethod]) throw Error(`unsupported guard classification: ${item.id}`);
    if (typeof item.capability!=='string' || !item.capability || !['parser','binder','checker','diagnostics'].includes(item.domain)) throw Error(`missing capability/domain: ${item.id}`);
    if (item.upstreamExecutableCaseId!==null) throw Error(`unverified upstream case mapping: ${item.id}`);
    if (item.implementation==='UNIMPLEMENTED' && item.implementationLocation!==null) throw Error(`unverified implementation location: ${item.id}`);
    if (item.implementation==='IMPLEMENTED' && !item.implementationLocation) throw Error(`missing implemented location: ${item.id}`);
    byMethod[item.guardMethod].push(item.source);
    classifyImplementation(item); // No exceptions or missing execution may be converted to SKIP.
  }
  for (const method of Object.keys(allowedMethods)) {
    const exactSources = extractInlineSources(source, method);
    if (!isDeepStrictEqual(byMethod[method], exactSources)) throw Error(`unregistered or reordered guard inputs: ${method}`);
  }
  const cases=registry.cases.map(({id,source,guardMethod,capability,domain,implementation,implementationLocation,skipReason,upstreamExecutableCaseId})=>({
    id,source,guardMethod,capability,domain,implementation,implementationLocation,
    ...classifyImplementation({id,implementation,skipReason}),
    upstreamExecutableCaseId
  }));
  const count=status=>cases.filter(x=>x.status===status).length;
  const byDomain=Object.fromEntries([...new Set(cases.map(x=>x.domain))].sort().map(d=>[d,{
    registered:cases.filter(x=>x.domain===d).length,
    implemented:cases.filter(x=>x.domain===d&&x.implementation==='IMPLEMENTED').length,
    skipUnimplemented:cases.filter(x=>x.domain===d&&x.status==='SKIP').length
  }]));
  return {
    schemaVersion:1,kind:registry.kind,upstreamCommit:registry.upstreamCommit,
    sourcePath,sourceGitBlob:registry.sourceGitBlob,
    scope:'LOCAL_GUARD_PROBES_ONLY',concreteUpstreamCases:null,upstreamCaseExpansion:'UNEXPANDED',
    fullCompilerAccepted:0,fullCompilerAcceptance:'NOT_RUN',releaseEligible:false,
    counts:{registered:cases.length,implemented:cases.filter(x=>x.implementation==='IMPLEMENTED').length,
      verifiedUpstream:0,skippedUnimplemented:count('SKIP'),notRun:count('NOT_RUN'),
      fail:0,mismatch:0,crash:0,timeout:0,upstreamSkipped:0},
    uniqueUnimplementedCapabilities:[...new Set(cases.filter(x=>x.status==='SKIP').map(x=>x.capability))].sort(),
    byDomain,cases,
    estimate:{sampleSnapshots:1,ratePerDay:null,remainingDays:null,confidence:'INSUFFICIENT_DATA',
      reason:'No two time-separated snapshots of a fixed upstream executable-case denominator exist.'}
  };
}

export function generateCoverage(cwd = process.cwd()) {
  const input = path => readFileSync(`${cwd}/${path}`);
  return buildCoverage(JSON.parse(input(registryPath).toString()), input(sourcePath), JSON.parse(input('upstream.lock.json').toString()));
}

if (process.argv[1]?.endsWith('implementation-coverage.ts')) {
  const flags = new Set(process.argv.slice(2));
  if ([...flags].some(x=>!['--check','--write','--release'].includes(x)) || flags.has('--check')&&flags.has('--write')) throw Error('usage: node tools/implementation-coverage.ts [--check|--write|--release]');
  const result = generateCoverage();
  const serialized = JSON.stringify(result,null,2)+'\n';
  if (flags.has('--write')) { mkdirSync('docs/testing/ts7',{recursive:true}); writeFileSync(reportPath,serialized); }
  if (flags.has('--check') && readFileSync(reportPath,'utf8')!==serialized) throw Error('stale implementation coverage report; run --write and review the scope');
  console.log(`Local guard probes: ${result.counts.implemented} implemented/NOT_RUN, ${result.counts.skippedUnimplemented} SKIP(UNIMPLEMENTED), ${result.counts.verifiedUpstream} full upstream accepted; upstream denominator unknown.`);
  if (flags.has('--release')) throw Error('release blocked: unimplemented skips and unexpanded upstream scope remain');
}
