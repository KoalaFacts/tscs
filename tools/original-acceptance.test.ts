import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, rmSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {
  parseUnits, missingCapabilities, baselineStem, baselineDiagnostics, baselineOutputs, compare,
  runTarget, summarize, headline, type Target, type TargetResult,
} from './original-acceptance.ts';

const target = (path: string, config: Record<string, string> | null, name = '', group = 'compiler'): Target => ({
  id: `configuration:${path}:${name}`, upstreamPath: path, rawSha256: '', group,
  configuration: config === null ? null : {Name: name, Config: config},
});

test('units follow the pinned compiler test parser', () => {
  assert.deepEqual(parseUnits('// @target: es2015\r\n// @noLib: true\r\n\r\nlet x = 1;\r\n', 'cases/compiler/a.ts'),
    [{name: 'a.ts', content: 'let x = 1;\n'}]);
  assert.deepEqual(parseUnits('// @target: es2015\n// @filename: a.ts\nexport const a = 1;\n// @Filename: b.ts\n\nimport {a} from "./a";',
    'cases/compiler/m.ts'), [{name: 'a.ts', content: 'export const a = 1;'}, {name: 'b.ts', content: 'import {a} from "./a";'}]);
  assert.throws(() => parseUnits('let x = 1;\n// @filename: a.ts\n', 'x.ts'), /non-comment content before the first @filename/);
});

test('missing capabilities are all recorded, and supported targets have none', () => {
  const units = [{name: 'a.ts', content: ''}];
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'es2015', nolib: 'true'}), units), []);
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'ES6', noLib: 'true', noemit: 'true', strict: 'true'}), units), []);
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'es2015'}), units), ['standard-library']);
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'es2015', nolib: 'true', lib: 'es5'}), units), [],
    '@lib with @noLib is the pinned TS5053 conflict, which tscs reports');
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'es2015', nolib: 'false', lib: 'es5'}), units), ['standard-library']);
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'es5', module: 'commonjs', strict: 'false', nolib: 'true'}),
    [{name: 'a.tsx', content: ''}, {name: 'b.d.ts', content: ''}]),
  ['option:target=es5', 'option:module', 'option:strict=false', 'input:.tsx', 'multi-file']);
  assert.deepEqual(missingCapabilities(target('a.ts', {target: 'esnext', nolib: 'true'}), units), [],
    'esnext runs; tscs fails closed (TSCS1002) where its ES2015 writer would lower');
  assert.deepEqual(missingCapabilities(target('a.ts', null), null), ['reference-fatal-source']);
  assert.deepEqual(missingCapabilities(target('a.ts', {}, '', 'transpile'), null), ['transpile-runner']);
});

test('baseline names, diagnostics and emitted sections follow the pinned runner', () => {
  assert.equal(baselineStem(target('tsc/x/emptyFile.ts', {}, 'target=es2015')), 'emptyFile(target=es2015)');
  assert.equal(baselineStem(target('tsc/x/emptyFile.ts', {})), 'emptyFile');
  assert.deepEqual(baselineDiagnostics(null), []);
  assert.deepEqual(baselineDiagnostics('error TS2318: Cannot find global type \'Array\'.\r\na.ts(1,5): error TS2322: Bad.\r\n\r\n\r\n==== a.ts (1 errors) ====\r\n    x\r\n'),
    ['error TS2318: Cannot find global type \'Array\'.', 'a.ts(1,5): error TS2322: Bad.']);
  const js = '//// [tests/cases/compiler/a.ts] ////\r\n\r\n//// [a.ts]\r\nlet x = 1;\r\n\r\n//// [a.js]\r\n"use strict";\r\nlet x = 1;\r\n';
  assert.deepEqual([...baselineOutputs(js, [{name: 'a.ts', content: 'let x = 1;'}])], [['a.js', '"use strict";\nlet x = 1;']]);
});

test('comparison reports diagnostic and emit differences', () => {
  const out = new Map([['a.js', 'x']]);
  assert.deepEqual(compare(['e1'], ['e1'], out, new Map([['a.js', 'x\r\n']])), []);
  assert.deepEqual(compare(['e1'], ['e2', 'e3'], new Map(), new Map()), ['diagnostics differ (1 missing, 2 unexpected)']);
  assert.deepEqual(compare([], [], new Map(), new Map([['a.js', 'x']])), ['emit unexpected: a.js']);
  assert.deepEqual(compare([], [], out, new Map([['a.js', 'y']])), ['emit differs: a.js']);
  assert.deepEqual(compare([], [], out, new Map()), ['emit missing: a.js']);
  assert.deepEqual(compare(['e1', 'e2'], ['e2', 'e1'], new Map(), new Map()), ['diagnostics differ (0 missing, 0 unexpected)']);
});

// A stand-in compiler: prints the diagnostics in a.ts's "// print:" lines,
// writes a.js from its "// emit:" line, and records each invocation.
const FAKE = `
const fs = require('node:fs');
fs.appendFileSync(process.env.FAKE_LOG, process.argv.slice(2).join(' ') + '\\n');
const files = process.argv.slice(2).filter((a) => a.endsWith('.ts'));
if (files.some((f) => fs.readFileSync(f, 'utf8').includes('// crash'))) process.exit(134);
if (files.some((f) => fs.readFileSync(f, 'utf8').includes('// silent2'))) process.exit(2);
if (files.some((f) => fs.readFileSync(f, 'utf8').includes('// stray'))) { fs.mkdirSync('nested'); fs.writeFileSync('nested/stray.js', ''); }
for (const f of files) {
  const text = fs.readFileSync(f, 'utf8');
  for (const m of text.matchAll(/\\/\\/ print: (.*)/g)) { console.log(m[1]); process.exitCode = m[1].includes('TSCS') ? 1 : 2; }
  const e = /\\/\\/ emit: (.*)/.exec(text);
  if (e && process.argv.includes('--emitJs')) fs.writeFileSync(f.replace(/\\.ts$/, '.js'), e[1] + '\\r\\n');
}`;

function upstream(cases: Record<string, string>, baselines: Record<string, string>) {
  const root = mkdtempSync(join(tmpdir(), 'tscs-original-test-'));
  for (const [name, text] of Object.entries(cases)) {
    mkdirSync(join(root, 'tsc/testdata/tests/cases/compiler'), {recursive: true});
    writeFileSync(join(root, 'tsc/testdata/tests/cases/compiler', name), text);
  }
  mkdirSync(join(root, 'tsc/testdata/baselines/reference/compiler'), {recursive: true});
  for (const [name, text] of Object.entries(baselines)) writeFileSync(join(root, 'tsc/testdata/baselines/reference/compiler', name), text);
  const fake = join(root, 'fake-tscs.cjs');
  writeFileSync(fake, FAKE);
  const log = join(root, 'invocations.log');
  process.env.FAKE_LOG = log;
  return {root, options: {upstream: root, tscs: fake, dotnet: process.execPath}, log};
}

test('an executed target passes only when diagnostics and emit match the reference', () => {
  const header = (n: string) => `//// [tests/cases/compiler/${n}.ts] ////\r\n\r\n//// [${n}.ts]\r\n// emit: "use strict";\r\n\r\n`;
  const {root, options, log} = upstream({
    'ok.ts': '// @target: es2015\n// @noLib: true\n// emit: "use strict";\n',
    'bad.ts': '// @target: es2015\n// @noLib: true\n// print: error TS2318: Cannot find global type \'Array\'.\n// emit: "use strict";\n',
    'emit.ts': '// @target: es2015\n// @noLib: true\n// emit: let x;\n',
    'unsupported.ts': '// @noLib: true\n// print: unsupported.ts(1,1): error TSCS1001: Not supported yet.\n',
    'lib.ts': '// @target: es2015\nlet x = 1;\n',
    'crash.ts': '// @noLib: true\n// @noEmit: true\n// crash\n',
    'extra.ts': '// @noLib: true\n// emit: "use strict";\n',
    'noref.ts': '// @target: es5\n// @noLib: true\n',
    'silent.ts': '// @noLib: true\n// @noEmit: true\n// silent2\n',
    'stray.ts': '// @noLib: true\n// @noEmit: true\n// stray\n',
    'withlib.ts': '// @lib: es5\n// @noLib: true\n// emit: "use strict";\n',
    'next.ts': '// @target: esnext\n// @noLib: true\n// emit: "use strict";\n',
  }, {
    ...Object.fromEntries(['ok', 'bad', 'emit', 'unsupported', 'crash', 'extra', 'lib', 'silent', 'stray', 'withlib', 'next'].map((n) => [`${n}.types`, ''])),
    'ok.js': `${header('ok')}//// [ok.js]\r\n"use strict";\r\n`,
    'bad.js': `${header('bad')}//// [bad.js]\r\n"use strict";\r\n`,
    'emit.js': `${header('emit')}//// [emit.js]\r\n"use strict";\r\n`,
    'withlib.js': `${header('withlib')}//// [withlib.js]\r\n"use strict";\r\n`,
    'next.js': `${header('next')}//// [next.js]\r\n"use strict";\r\n`,
  });
  try {
    const path = (n: string) => `tsc/testdata/tests/cases/compiler/${n}`;
    const sha = (n: string) => createHash('sha256').update(readFileSync(join(root, path(n)))).digest('hex');
    const run = (n: string, c: Record<string, string>) => runTarget({...target(path(n), c), rawSha256: sha(n)}, options);
    assert.deepEqual(run('ok.ts', {target: 'es2015', nolib: 'true'}).status, 'PASS');
    assert.deepEqual(run('bad.ts', {target: 'es2015', nolib: 'true'}).reasons, ['diagnostics differ (0 missing, 1 unexpected)']);
    assert.deepEqual(run('emit.ts', {target: 'es2015', nolib: 'true'}).reasons, ['emit differs: emit.js']);
    assert.deepEqual(run('unsupported.ts', {nolib: 'true'}).reasons, ['tscs TSCS1001: Not supported yet.']);
    assert.deepEqual(run('crash.ts', {nolib: 'true', noemit: 'true'}).reasons, ['tscs exited with 134'],
      'a crash is never a PASS, even when the reference expects no output');
    assert.deepEqual(run('extra.ts', {nolib: 'true'}).reasons, ['emit unexpected: extra.js'],
      'without a .js baseline the reference emitted nothing');
    assert.deepEqual(run('silent.ts', {nolib: 'true', noemit: 'true'}).reasons, ['tscs exit 2 disagrees with 0 diagnostic line(s)']);
    assert.deepEqual(run('stray.ts', {nolib: 'true', noemit: 'true'}).reasons, ['emit unexpected: nested/stray.js'],
      'outputs are collected recursively');
    const noReference = run('noref.ts', {nolib: 'true'});
    assert.deepEqual([noReference.status, noReference.reasons], ['SKIP', ['no-reference-baseline']],
      'a configuration the reference never ran is not compared against empty expectations');
    assert.throws(() => runTarget({...target(path('ok.ts'), {nolib: 'true'}), rawSha256: '0'.repeat(64)}, options),
      /does not match the registered pinned source/);
    const skipped = run('lib.ts', {target: 'es2015'});
    assert.deepEqual([skipped.status, skipped.reasons], ['SKIP', ['standard-library']]);
    assert.equal(run('withlib.ts', {lib: 'es5', nolib: 'true'}).status, 'PASS');
    assert.equal(run('next.ts', {target: 'esnext', nolib: 'true'}).status, 'PASS');
    const invocations = readFileSync(log, 'utf8').trim().split('\n');
    assert.equal(invocations.length, 10, 'a SKIP target never runs the compiler');
    assert.ok(invocations[9].includes('--emitJs --noLib --target esnext'), 'the @target value is passed through');
    assert.ok(invocations[8].includes('--emitJs --noLib --lib es5 --target es2015'), 'the @lib list is passed through');
    assert.ok(invocations.slice(0, 4).every((l) => l.includes('--emitJs --noLib --target es2015')));
    assert.ok(invocations[4].includes('--noEmit --noLib --target es2015'));
    assert.ok(invocations[5].includes('--emitJs'));
    assert.ok(!existsSync(join(root, 'tsc/testdata/tests/cases/compiler/ok.js')), 'upstream checkout is not written');
  } finally {
    rmSync(root, {recursive: true, force: true});
  }
});

test('the report counts every status and both capability histograms', () => {
  const r = (status: TargetResult['status'], reasons: string[]): TargetResult => ({id: 'x', upstreamPath: 'p', configuration: '', status, reasons});
  const report = summarize('abc', [
    r('PASS', []), r('FAIL', ['diagnostics differ (1 missing, 0 unexpected)']), r('FAIL', ['tscs TSCS1001: X']),
    r('SKIP', ['standard-library']), r('SKIP', ['standard-library', 'option:target=es5']), r('SKIP', ['option:target=es3']),
  ]);
  assert.deepEqual(report.totals, {PASS: 1, FAIL: 2, SKIP: 3});
  assert.deepEqual(report.missingCapabilities, {'option:target': 2, 'standard-library': 2});
  assert.deepEqual(report.soleMissingCapability, {'option:target': 1, 'standard-library': 1});
  assert.deepEqual(report.failures, {'diagnostics differ': 1, 'tscs TSCS1001': 1});
  assert.equal(headline({PASS: 0, FAIL: 2, SKIP: 15521}), '0 PASS / 2 FAIL / 15,521 SKIP');
});
