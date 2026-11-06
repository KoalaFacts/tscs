import assert from 'node:assert/strict';
import {mkdirSync, mkdtempSync, writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import test from 'node:test';
import {bindLibs, libFiles, parseLibs, requiredClosure, summarize, type FileResult} from './check-standard-library.ts';

// Stands in for the fixture harness: one JSONL response per request; a lib
// whose text contains "BAD" gets a diagnostic, references are read back;
// a binding request reports TS2300 for every file containing "DUP" and an
// unresolved TS2304 for every file containing "MISSING".
const FAKE = `
const lines = require('node:fs').readFileSync(0, 'utf8').split('\\n').filter(Boolean);
for (const line of lines) {
  const row = JSON.parse(line);
  if (row.profile === 'declaration-binding') {
    const texts = row.files.map((f) => ({file: f.fileName, text: Buffer.from(f.input, 'base64').toString('utf8')}));
    const diagnostics = texts.filter((t) => t.text.includes('DUP')).map((t) =>
      ({file: t.file, code: 'TS2300', start: t.text.indexOf('DUP'), message: "Duplicate identifier 'DUP'."}));
    const unresolved = texts.filter((t) => t.text.includes('MISSING')).map((t) =>
      ({file: t.file, code: 'TS2304', start: t.text.indexOf('MISSING'), message: "Cannot find name 'MISSING'."}));
    console.log(JSON.stringify({id: row.id, expected: {symbols: texts.length, merged: 0, diagnostics,
      ...(texts.some((t) => t.text.includes('NO_UNRESOLVED_FIELD')) ? {} : {unresolved})}}));
    continue;
  }
  if (row.profile !== 'declaration-syntax') process.exit(3);
  const text = Buffer.from(row.input, 'base64').toString('utf8');
  const references = [...text.matchAll(/<reference lib="([^"]+)"/g)].map((m) => ({name: 'lib', value: m[1]}));
  const diagnostics = text.includes('BAD') ? [{code: 'TS1005', start: text.indexOf('BAD'), message: "';' expected."}] : [];
  console.log(JSON.stringify({id: row.id, expected: {statements: 1, references, diagnostics}}));
}`;

function libs(files: Record<string, string>) {
  const root = mkdtempSync(join(tmpdir(), 'tscs-stdlib-test-'));
  const dir = join(root, 'libs');
  mkdirSync(dir);
  for (const [name, text] of Object.entries(files)) writeFileSync(join(dir, name), text);
  writeFileSync(join(root, 'fake-harness.cjs'), FAKE);
  return {dir, options: {dotnet: process.execPath, harness: join(root, 'fake-harness.cjs')}};
}

const result = (file: string, libReferences: string[], bad = false): FileResult =>
  ({file, statements: 1, libReferences, diagnostics: bad ? [{code: 'TS1005', start: 0, message: "';' expected."}] : []});

test('only lib.*.d.ts files are parsed, in a stable order', () => {
  const {dir} = libs({'lib.b.d.ts': '', 'lib.a.d.ts': '', 'README.md': '', 'other.d.ts': ''});
  assert.deepEqual(libFiles(dir), ['lib.a.d.ts', 'lib.b.d.ts']);
});

test('the required set follows lib reference directives from the roots', () => {
  const results = [result('lib.es5.d.ts', ['lib.decorators.d.ts']), result('lib.decorators.d.ts', []),
    result('lib.es2015.d.ts', ['lib.es5.d.ts', 'lib.es2015.core.d.ts']), result('lib.es2015.core.d.ts', []),
    result('lib.dom.d.ts', [])];
  assert.deepEqual(requiredClosure(results, ['lib.es5.d.ts', 'lib.es2015.d.ts']),
    ['lib.decorators.d.ts', 'lib.es2015.core.d.ts', 'lib.es2015.d.ts', 'lib.es5.d.ts']);
  assert.throws(() => requiredClosure([result('lib.es5.d.ts', ['lib.missing.d.ts'])], ['lib.es5.d.ts']), /lib.missing.d.ts/);
});

test('a diagnostic in a required file is reported as not clean; others only count', () => {
  const {dir, options} = libs({
    'lib.es5.d.ts': '/// <reference lib="decorators" />\ninterface A {}',
    'lib.decorators.d.ts': 'interface B { x BAD }',
    'lib.dom.d.ts': 'BAD',
    'lib.es2015.d.ts': '/// <reference lib="es5" />',
  });
  const report = summarize(parseLibs(dir, libFiles(dir), options), ['lib.es5.d.ts', 'lib.es2015.d.ts']);
  assert.equal(report.libs, 4);
  assert.equal(report.clean, 2);
  assert.deepEqual(report.required, [{file: 'lib.decorators.d.ts', clean: false}, {file: 'lib.es2015.d.ts', clean: true},
    {file: 'lib.es5.d.ts', clean: true}]);
  assert.deepEqual(report.firstDiagnostics, {"TS1005 ';' expected.": ['lib.decorators.d.ts', 'lib.dom.d.ts']});
});

test('a failing harness is an error, never a clean result', () => {
  const {dir} = libs({'lib.es5.d.ts': ''});
  assert.throws(() => parseLibs(dir, libFiles(dir), {dotnet: process.execPath, harness: join(dir, 'missing.cjs')}), /harness exited/);
});

test('the closure is bound in one request and binder diagnostics are reported per file', () => {
  const {dir, options} = libs({'lib.a.d.ts': 'interface A {}', 'lib.b.d.ts': 'type DUP = 1;'});
  const binding = bindLibs(dir, ['lib.a.d.ts', 'lib.b.d.ts'], options);
  assert.equal(binding.files, 2);
  assert.equal(binding.symbols, 2);
  assert.deepEqual(binding.diagnostics, [{file: 'lib.b.d.ts', code: 'TS2300', start: 5, message: "Duplicate identifier 'DUP'."}]);
  assert.deepEqual(binding.unresolved, []);
  assert.throws(() => bindLibs(dir, ['lib.a.d.ts'], {...options, harness: join(dir, 'missing.cjs')}), /harness exited/);
});

test('unresolved names are reported per file, and a response without them is an error', () => {
  const {dir, options} = libs({'lib.a.d.ts': 'interface A { b: MISSING; }', 'lib.b.d.ts': 'interface B {}',
    'lib.c.d.ts': 'NO_UNRESOLVED_FIELD'});
  const binding = bindLibs(dir, ['lib.a.d.ts', 'lib.b.d.ts'], options);
  assert.deepEqual(binding.diagnostics, []);
  assert.deepEqual(binding.unresolved, [{file: 'lib.a.d.ts', code: 'TS2304', start: 17, message: "Cannot find name 'MISSING'."}]);
  assert.throws(() => bindLibs(dir, ['lib.c.d.ts'], options), /unresolved/);
});
