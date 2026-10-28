import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';

// This is a local bounded semantic profile, NOT an original TS7 test runner.
// Compare real diagnostic code arrays (including negative cases), then execute
// the emitted program on both sides. Formatting/spans and full harness output
// are deliberately not counted as matching by this profile.
const [referenceArgument, outputArgument] = process.argv.slice(2);
assert.ok(referenceArgument && outputArgument, 'usage: node tools/verify-object-data-reference.ts <pinned-reference> <new-output-dir>');
const root = process.cwd();
const reference = resolve(referenceArgument);
const output = resolve(outputArgument);
assert.ok(!existsSync(output), 'refusing to overwrite evidence');
mkdirSync(output, { recursive: true });
const pin = JSON.parse(readFileSync('upstream.lock.json', 'utf8'));
const product = resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const suitePath = 'fixtures/object-data/semantic-cases.json';
const suite = JSON.parse(readFileSync(suitePath, 'utf8'));
assert.equal(suite.kind, 'BOUNDED_OBJECT_SEMANTICS_NOT_ORIGINAL_TS7_ACCEPTANCE');
assert.equal(suite.cases.length, 29, 'the frozen bounded cohort must not shrink');
const hash = (bytes: string | Buffer) => createHash('sha256').update(bytes).digest('hex');
const artifacts: { path: string; sha256: string; length: number }[] = [];
function save(path: string, content: string) {
  const bytes = Buffer.from(content);
  writeFileSync(join(output, path), bytes, { flag: 'wx', flush: true });
  artifacts.push({ path, sha256: hash(bytes), length: bytes.length });
}
function command(exe: string, args: string[], cwd: string) {
  const result = spawnSync(exe, args, { cwd, encoding: 'utf8', timeout: 30000, maxBuffer: 4 * 1024 * 1024,
    env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', TESTINGPLATFORM_TELEMETRY_OPTOUT: '1',
      GOTOOLCHAIN: 'local', GOPROXY: 'off', GOSUMDB: 'off', GOWORK: 'off' } });
  // Timeout, launch failures and signals remain failures, never SKIP.
  assert.equal(result.error, undefined, String(result.error));
  assert.equal(result.signal, null);
  assert.notEqual(result.status, null);
  return { command: [exe, ...args], status: result.status, stdout: result.stdout, stderr: result.stderr };
}
const version = command(reference, ['--version'], root);
assert.equal(version.status, 0);
assert.ok(version.stdout.includes(pin.version), `reference version mismatch: ${version.stdout}`);
save('reference-version.json', JSON.stringify(version, null, 2));
const sourceSha = command('git', ['rev-parse', 'HEAD'], root).stdout.trim();
const globals = readFileSync('fixtures/minimal.d.ts', 'utf8');
const ids = new Set<string>();
const results: object[] = [];
const mismatches: object[] = [];
for (let index = 0; index < suite.cases.length; index++) {
  const item = suite.cases[index];
  assert.match(item.id, /^object-data:(positive|negative)-\d+$/);
  assert.ok(!ids.has(item.id)); ids.add(item.id);
  const folder = join(output, `case-${String(index).padStart(3, '0')}`);
  mkdirSync(folder);
  writeFileSync(join(folder, 'input.ts'), item.source);
  writeFileSync(join(folder, 'minimal.d.ts'), globals);
  const args = ['--noEmit', '--noLib', '--target', 'es2015', '--pretty', 'false', 'input.ts', 'minimal.d.ts'];
  const expected = command(reference, args, folder);
  const actual = command(process.env.DOTNET ?? 'dotnet', [product, ...args], folder);
  const codes = (run: typeof expected) => [...(run.stdout + '\n' + run.stderr).matchAll(/error (TS\d+):/g)]
    .map(match => match[1]).sort();
  save(`case-${index}-reference.json`, JSON.stringify(expected, null, 2));
  save(`case-${index}-actual.json`, JSON.stringify(actual, null, 2));
  const matched = expected.status === (item.expectedCodes.length === 0 ? 0 : 2) &&
    actual.status === expected.status &&
    JSON.stringify(codes(expected)) === JSON.stringify([...item.expectedCodes].sort()) &&
    JSON.stringify(codes(actual)) === JSON.stringify(codes(expected));
  const row = { id: item.id, sourceSha256: hash(item.source), referenceCodes: codes(expected), actualCodes: codes(actual),
    expectedCodes: item.expectedCodes, referenceExit: expected.status, actualExit: actual.status,
    outcome: matched ? 'MATCH_EXIT_AND_DIAGNOSTIC_CODES' : 'MISMATCH', originalAcceptanceCredit: false };
  results.push(row);
  if (!matched) mismatches.push(row);
}
const graph = {
  'data.ts': 'export const seed = { value: 20, label: "seed", nested: { enabled: true } };',
  'transform.ts': 'export function twice(data: { value: number }): { value: number } { return { value: data.value * 2 }; }',
  'bridge.ts': 'export { twice as transform } from "./transform.js";',
  'entry.ts': 'import { seed } from "./data.js"; import { transform } from "./bridge.js"; seed.value = 21; seed.nested.enabled = false; export const result: number = transform(seed).value; export const label: string = seed.label; export const enabled: boolean = seed.nested.enabled;'
};
for (const side of ['reference', 'actual']) {
  const folder = join(output, side + '-runtime'); mkdirSync(folder);
  for (const [name, source] of Object.entries(graph)) writeFileSync(join(folder, name), source);
  writeFileSync(join(folder, 'minimal.d.ts'), globals);
  writeFileSync(join(folder, 'package.json'), '{"type":"module"}\n');
  const args = ['--noLib', '--target', 'es2015', '--module', 'es2015', '--pretty', 'false', 'entry.ts', 'minimal.d.ts'];
  const compiled = side === 'reference' ? command(reference, args, folder) :
    command(process.env.DOTNET ?? 'dotnet', [product, '--emitJs', ...args], folder);
  save(side + '-compile.json', JSON.stringify(compiled, null, 2));
  assert.equal(compiled.status, 0, compiled.stdout + compiled.stderr);
  for (const name of ['data', 'transform', 'bridge', 'entry']) assert.ok(existsSync(join(folder, name + '.js')));
  const runtime = command(process.execPath, ['--input-type=module', '--eval',
    'import { result, label, enabled } from "./entry.js"; if (result !== 42 || label !== "seed" || enabled !== false) throw Error("runtime mismatch"); console.log(JSON.stringify({result,label,enabled}));'], folder);
  save(side + '-runtime.json', JSON.stringify(runtime, null, 2));
  assert.equal(runtime.status, 0, runtime.stdout + runtime.stderr);
  assert.deepEqual(JSON.parse(runtime.stdout), { result: 42, label: 'seed', enabled: false });
}
// Preserve inputs and emitted bytes, not only a green counter.
for (const directory of readdirSync(output, { withFileTypes: true }).filter(item => item.isDirectory())) {
  for (const name of readdirSync(join(output, directory.name))) {
    const bytes = readFileSync(join(output, directory.name, name));
    artifacts.push({ path: directory.name + '/' + name, sha256: hash(bytes), length: bytes.length });
  }
}
save('results.json', JSON.stringify(results, null, 2));
save('summary.json', JSON.stringify({ kind: suite.kind, sourceSha, upstreamCommit: pin.commit,
  referenceVersion: version.stdout.trim(), referenceSha256: hash(readFileSync(reference)),
  productCliSha256: hash(readFileSync(product)),
  productCompilerSha256: hash(readFileSync(join(root, 'src/Tscs.Compiler/bin/Release/net10.0/Tscs.Compiler.dll'))),
  cohortSha256: hash(readFileSync(suitePath)), scriptSha256: hash(readFileSync(import.meta.filename)),
  semanticMatches: results.length - mismatches.length, semanticMismatches: mismatches.length, runtimeMatches: 1, originalAcceptanceCredit: 0,
  comparison: 'exit, diagnostic code arrays and runtime value; NOT full diagnostics, formatting or original harness acceptance',
  referenceExecutable: basename(reference) }, null, 2));
// Verify all bytes after process completion before finalizing their manifest.
for (const item of artifacts) {
  const bytes = readFileSync(join(output, item.path));
  assert.equal(bytes.length, item.length); assert.equal(hash(bytes), item.sha256);
}
writeFileSync(join(output, 'manifest.json'), JSON.stringify({ artifacts }, null, 2), { flag: 'wx', flush: true });
assert.deepEqual(mismatches, [], "bounded object reference mismatches (raw results preserved)");
console.log(`${results.length} bounded object semantics matched pinned ${pin.commit}; both emitted module graphs executed result=42. Original TS7 acceptance unchanged.`);
