import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { parseTree, classifyAsset, inspectFixture, initialProgress, validateProgress, stableId, summarize } from './ts7-scope.mjs';

const oid = 'a'.repeat(40), pin = 'b'.repeat(40);
const asset = (path = 'tsc/testdata/tests/cases/compiler/a.ts') => ({id: stableId(pin, 'asset', path), path, mode:'100644', oid, sha256:'c'.repeat(64), domain:'fixture:compiler', role:'fixture-input'});
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

test('tree parser preserves byte-exact paths, modes, and gitlinks', () => {
  assert.deepEqual(parseTree(Buffer.from(`100644 blob ${oid}\tspace and\ttab.ts\0` + `120000 blob ${oid}\tlink\0` + `160000 commit ${oid}\tvendor/ts\0`)), [
    {mode:'100644', type:'blob', oid, path:'space and\ttab.ts'},
    {mode:'120000', type:'blob', oid, path:'link'},
    {mode:'160000', type:'commit', oid, path:'vendor/ts'},
  ]);
});

test('tree parser rejects duplicates, traversal, malformed and undecodable paths', () => {
  const row = p => Buffer.from(`100644 blob ${oid}\t${p}\0`);
  for (const input of [Buffer.concat([row('x'),row('x')]),row('../outside'),row('/outside'),row('a/../b'),Buffer.from('bad\0'),Buffer.concat([Buffer.from(`100644 blob ${oid}\t`),Buffer.from([255,0])])]) {
    assert.throws(() => parseTree(input));
  }
});

test('IDs bind pin, record kind, and full path rather than basename', () => {
  assert.match(stableId(pin,'asset','a/x.ts'), /^asset:[a-f0-9]{64}$/);
  assert.equal(stableId(pin,'asset','a/x.ts'), stableId(pin,'asset','a/x.ts'));
  assert.notEqual(stableId(pin,'asset','a/x.ts'),stableId(pin,'asset','b/x.ts'));
  assert.notEqual(stableId(pin,'asset','a/x.ts'),stableId(oid,'asset','a/x.ts'));
});

test('classification separates runner input, support, baselines, tools and JS tests', () => {
  const cases = [
    ['tsc/testdata/tests/cases/compiler/a.ts','fixture-input','fixture:compiler'],
    ['tsc/testdata/tests/cases/compiler/b.js','fixture-support','fixture:compiler'],
    ['tsc/testdata/tests/cases/transpile/a.mts','fixture-input','fixture:transpile'],
    ['tsc/testdata/baselines/reference/tsbuild/a.js','baseline','baseline:tsbuild'],
    ['tsc/internal/fourslash/tests/x_test.go','go-test-source','go:tsc/internal/fourslash/tests'],
    ['tools/customlint/testdata/typedpaths/typedpaths_test.go','go-test-fixture','support:tools/customlint/testdata'],
    ['packages/api/test/client.test.ts','js-test-source','js:packages/api'],
    ['unknown/new-suite/data.weird','upstream-support','support:unknown'],
  ];
  for (const [path,role,domain] of cases) assert.deepEqual(classifyAsset(path),{role,domain});
});

test('fixture metadata preserves duplicate raw directives and does not invent variants', () => {
  const bytes = Buffer.from('// @target: es5, esnext\n// @target: es2022\n// @Filename: ../virtual.ts\n// @link: /a -> /b\n// @lib: es5, dom\nlet x = 1;\n');
  const result = inspectFixture(asset(),bytes);
  assert.deepEqual(result.directives.map(x => [x.name,x.value,x.line]),[
    ['target','es5, esnext',1],['target','es2022',2],['filename','../virtual.ts',3],['link','/a -> /b',4],['lib','es5, dom',5],
  ]);
  assert.equal(result.expansion.status,'UNEXPANDED');
  assert.equal(result.expansion.concreteCases,null);
  assert.equal(result.virtualPathDeclarations,1);
  assert.equal(result.symlinkDeclarations,1);
  assert.equal(result.progress.compiler,'NOT_RUN');
  assert.equal(result.progress.acceptance,'NOT_RUN');
});

test('metadata follows anchored upstream option syntax rather than accepting indentation', () => {
  const result = inspectFixture(asset(),Buffer.from(' // @target: es5\n// @strict: true\r\n// @noEmit: false\n'));
  assert.deepEqual(result.directives.map(x=>x.name),['strict','noemit']);
});

test('invalid UTF8 and UTF16 are byte evidence, never replacement text', () => {
  for (const bytes of [Buffer.from([0xff,0xfe,0x2f,0,0x2f,0]),Buffer.from([0x2f,0x2f,0x20,0xff])]) {
    const result = inspectFixture(asset(),bytes);
    assert.equal(result.decoding,'UTF8_INVALID');
    assert.deepEqual(Buffer.from(result.rawBytesBase64,'base64'),bytes);
    assert.equal(result.rawSha256,hash(bytes));
    assert.deepEqual(result.directives,[]);
    assert.ok(result.expansion.blockers.includes('BYTE_DECODING_UNIMPLEMENTED'));
  }
});

test('NUL-containing source and UTF8 BOM remain distinguishable raw evidence', () => {
  const bytes = Buffer.from('\uFEFF// @target: esnext\nlet x = "\0";');
  const result = inspectFixture(asset(),bytes);
  assert.equal(result.hasNul,true);
  assert.equal(result.bom,'UTF8');
  assert.equal(result.rawSha256,hash(bytes));
  assert.equal(result.expansion.concreteCases,null);
});

test('initial status is independent for each record and never implies acceptance', () => {
  const first=initialProgress(), second=initialProgress();
  assert.deepEqual(second,{implementation:'UNIMPLEMENTED',compiler:'NOT_RUN',reference:'NOT_RUN',acceptance:'NOT_RUN',evidence:[]});
  first.evidence.push({});
  assert.deepEqual(second.evidence,[]);
  assert.equal(validateProgress(second),true);
});

test('successful progress requires evidence and acceptance cannot outrun compiler/reference', () => {
  assert.throws(()=>validateProgress({...initialProgress(),compiler:'PASS'}),/evidence/i);
  const evidence=[{kind:'compiler',uri:'evidence/compiler.json',sha256:'1'.repeat(64),upstreamCommit:pin}];
  assert.throws(()=>validateProgress({...initialProgress(),acceptance:'PASS',evidence}),/acceptance/i);
  assert.throws(()=>validateProgress({...initialProgress(),compiler:'present'}),/status/i);
  assert.throws(()=>validateProgress({...initialProgress(),evidence:[{kind:'compiler'}]}),/evidence/i);
});

test('summary is lossless by domain but refuses to substitute files for executable cases', () => {
  const assets=[asset(),{...asset('elsewhere/readme.md'),role:'upstream-support',domain:'support:elsewhere'}];
  const fixtures=[inspectFixture(assets[0],Buffer.from('// @strict: true\n'))];
  const result=summarize(assets,fixtures,[]);
  assert.equal(result.trackedAssets,2);
  assert.equal(result.fixtureInputs,1);
  assert.equal(result.concreteCases,null);
  assert.equal(result.sourceScopeComplete,false);
  assert.equal(result.releaseEligible,false);
  assert.equal(result.domains.reduce((n,d)=>n+d.assets,0),2);
});

test('summary rejects dropped, duplicate and dangling fixture/declaration sources', () => {
  const a=asset(), f=inspectFixture(a,Buffer.from('let x=1;'));
  assert.throws(()=>summarize([a],[],[]),/fixture/i);
  assert.throws(()=>summarize([a],[f,f],[]),/duplicate/i);
  assert.throws(()=>summarize([a],[f],[{assetId:'absent',id:'declaration:x'}]),/source/i);
  assert.throws(()=>summarize([a,a],[f],[]),/duplicate/i);
});

test('UTF8 BOM handling and whitespace match the pinned reader/metadata syntax', () => {
  const result=inspectFixture(asset(),Buffer.from('\uFEFF// @target: esnext\n//\u00A0@strict: true\n'));
  assert.deepEqual(result.directives.map(x=>[x.name,x.value]),[['target','esnext']]);
});

test('metadata starts only at LF boundaries, not JS-only line separators', () => {
  for(const separator of ['\r','\u2028','\u2029']) {
    const result=inspectFixture(asset(),Buffer.from(`let x=1;${separator}// @filename: phantom.ts\n// @strict: true\n`));
    assert.deepEqual(result.directives.map(x=>[x.name,x.line]),[['strict',2]]);
    assert.equal(result.virtualPathDeclarations,0);
  }
});
