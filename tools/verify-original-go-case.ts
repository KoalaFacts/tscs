import {readFileSync,writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';

// Frozen, source-derived Go TestLocal/emptyFile.ts assertions at the exact pin.
// Go testing.T.Run normalizes whitespace in names to underscores.
// Types/symbols are checked within the target body, not in a Go t.Run child.
// Module resolution is conditional on TraceResolution and is not expected here.
export const originalGoPin = '8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f';
export const originalGoTargetId = 'configuration:c5f81e2689b2db32a694b7b3de45098f5d58941800bfe0683e2f68000e24c110';
export const originalGoPackage = 'github.com/microsoft/TypeScript/tsc/internal/testrunner';
export const originalGoTarget = 'TestLocal/emptyFile.ts';
export const requiredSubtests = Object.freeze([
  'error', 'content_mapper', 'output', 'sourcemap', 'sourcemap_record',
  'union_ordering', 'source_file_parent_pointers',
]);

export function inspectPinnedEmptyFileGoRun(jsonl, exitCode) {
  if (!Number.isInteger(exitCode) || exitCode !== 0) throw Error(`Go reference process exit ${exitCode}; not accepted`);
  if (typeof jsonl !== 'string' || !jsonl.endsWith('\n')) throw Error('Missing final JSONL newline; reference output may be truncated');
  const lines = jsonl.slice(0,-1).split('\n');
  if (lines.length === 0 || lines.some(line => line.trim() === '')) throw Error('Missing or empty Go JSONL event');
  const expectedNames = ['TestLocal', originalGoTarget, ...requiredSubtests.map(name => `${originalGoTarget}/${name}`)];
  const expected = new Set(expectedNames);
  const counts = new Map(expectedNames.map(name => [name, {run:0,pass:0,skip:0,fail:0}]));
  let packagePass=0, packageFail=0, packageSkip=0;
  for (const [index,line] of lines.entries()) {
    let event;
    try { event=JSON.parse(line); } catch { throw Error(`Malformed Go JSONL at line ${index+1}`); }
    if (!event || Array.isArray(event) || typeof event !== 'object' || typeof event.Action !== 'string') throw Error(`Malformed Go event at line ${index+1}`);
    if (event.Package !== originalGoPackage) throw Error(`Unexpected Go package at line ${index+1}: ${event.Package}`);
    if (event.Test !== undefined) {
      if (typeof event.Test !== 'string' || !expected.has(event.Test)) throw Error(`Unexpected Go test selected: ${event.Test}`);
      if (['run','pass','skip','fail'].includes(event.Action)) counts.get(event.Test)[event.Action]++;
    } else {
      if (event.Action === 'pass') packagePass++;
      if (event.Action === 'fail') packageFail++;
      if (event.Action === 'skip') packageSkip++;
    }
  }
  for (const [name, state] of counts) {
    if (state.run === 0 || state.pass === 0) throw Error(`Missing required Go run/pass: ${name}`);
    if (state.run !== 1 || state.pass !== 1) throw Error(`Duplicate Go run/pass: ${name}`);
    if (state.skip || state.fail) throw Error(`Go reference skip/fail: ${name}`);
  }
  if (packagePass !== 1 || packageFail || packageSkip) throw Error('Go reference package did not pass exactly once');
  return {
    schemaVersion:1,
    kind:'PINNED_GO_ORIGINAL_HARNESS_REFERENCE_ONLY',
    upstreamCommit:originalGoPin,
    targetId:originalGoTargetId,
    sourcePath:'tsc/testdata/tests/cases/compiler/emptyFile.ts',
    target:originalGoTarget,
    expectedSubtests:[...requiredSubtests],
    executedTestCount:expectedNames.length,
    eventCount:lines.length,
    originalReference:'PASS',
    productAcceptance:'NOT_RUN',
    originalTargetSkipRemoved:false,
    fullUpstreamAccepted:0,
    releaseEligible:false,
    limitations:['Go JSON events prove nested assertion execution but not a tscs product-side equivalent for types/symbols, module resolution, union invariants or parent pointers', 'Complete original product runner acceptance remains NOT_RUN'],
  };
}

function main(args) {
  if (args.length !== 3) throw Error('Usage: node tools/verify-original-go-case.ts GO_TEST_JSONL GO_EXIT_CODE NEW_EVIDENCE_JSON');
  const [input, exitText, output]=args;
  if (!/^(0|[1-9][0-9]*)$/.test(exitText)) throw Error('Invalid Go exit code argument');
  const raw=readFileSync(input);
  const log=raw.toString('utf8');
  if (!Buffer.from(log,'utf8').equals(raw)) throw Error('Go JSONL is not well-formed UTF-8');
  const data=inspectPinnedEmptyFileGoRun(log,Number(exitText));
  const report={...data,rawLog:{sha256:createHash('sha256').update(raw).digest('hex'),bytes:raw.length},
    command:["go","test","-json","-count=1","-run","^TestLocal/emptyFile[.]ts$","./internal/testrunner"],
    environment:{GOPROXY:'off',GOSUMDB:'off',GOTOOLCHAIN:'local',GOWORK:'off',GOTELEMETRY:'off'},
    referenceRunnerSource:'tsc/internal/testrunner/compiler_runner.go',
    sourceOriginalConfiguration:{target:'es2015'},
  };
  writeFileSync(output,JSON.stringify(report,null,2)+'\n',{flag:'wx'});
  console.log(JSON.stringify({kind:report.kind,target:report.target,reference:report.originalReference,product:report.productAcceptance,events:report.eventCount,sha256:report.rawLog.sha256}));
}
if(process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href){
  try{main(process.argv.slice(2));}catch(error){console.error(error.stack ?? String(error));process.exitCode=1;}
}
