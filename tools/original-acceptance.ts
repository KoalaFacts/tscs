// Original upstream acceptance runner.
//
// Runs the 15,523 registered original targets (targets.jsonl from the
// committed placeholder archive) against a checkout of the pinned upstream
// revision. A target is executed through tscs only when every capability it
// needs is supported; otherwise it is SKIP with the missing capabilities
// recorded. An executed target is PASS only when tscs's diagnostics equal the
// pinned reference baseline (<name>.errors.txt, or no errors when that file
// is absent) and, where the reference has a <name>.js baseline, every emitted
// output section equals tscs's emitted file. This includes safe multi-file
// inputs and explicit ES2015 modules; virtual symlinks and unsupported module
// transforms remain SKIP. `.types`/`.symbols` baselines are not compared. Usage (from the repository root):
//
//   node tools/original-acceptance.ts --targets <targets.jsonl> \
//     --upstream <TypeScript checkout> --tscs <tscs.dll> --out <report.json> \
//     [--expect-headline docs/STATUS.md]
//
// With --expect-headline the run fails when the measured totals differ from
// that page's "Original upstream acceptance" headline.
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync, mkdirSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {basename, dirname, extname, join, normalize, relative, resolve, sep} from 'node:path';
import {fileURLToPath} from 'node:url';
import {headlineOf} from './check-docs.ts';

export interface Target {
  id: string;
  upstreamPath: string;
  /** SHA-256 of the original test file at the pinned revision. */
  rawSha256: string;
  group: string;
  configuration: {Name: string; Config: Record<string, string>} | null;
}

export interface Unit {name: string; content: string}

export type Status = 'PASS' | 'FAIL' | 'SKIP';

export interface TargetResult {
  id: string;
  upstreamPath: string;
  configuration: string;
  status: Status;
  /** SKIP: every missing capability. FAIL: why the comparison differed. */
  reasons: string[];
}

const OPTION = /^\/{2}\s*@(\w+)\s*:\s*([^\r\n]*)/;
const LINK = /^\/{2}\s*@link\s*:/i;

/**
 * Splits an original test file into units, following the pinned
 * ParseTestFilesAndSymlinks for compiler tests: option lines are removed,
 * `@filename` starts a unit, and leading blank lines of a unit are dropped.
 */
export function parseUnits(code: string, fileName: string): Unit[] {
  const units: Unit[] = [];
  let name = '';
  let content = '';
  for (const line of code.split(/\r?\n/)) {
    if (LINK.test(line)) continue;
    const m = OPTION.exec(line);
    if (m) {
      if (m[1].toLowerCase() !== 'filename') continue;
      if (name !== '') units.push({name, content});
      else if (/\S/.test(content.replace(/\/\/[^\n]*|\/\*[\s\S]*?\*\//g, ''))) {
        throw new Error(`${fileName}: non-comment content before the first @filename`);
      }
      name = m[2].trim();
      content = '';
      continue;
    }
    if (content.length !== 0) content += '\n';
    content += line;
  }
  units.push({name: name === '' ? basename(fileName) : name, content});
  return units;
}

const SUPPORTED_TARGETS = new Set(['es2015', 'es6']);
const SUPPORTED_MODULES = new Set(['es2015', 'es6']);

/** Every capability the target needs that tscs does not provide. */
export function missingCapabilities(target: Target, units: Unit[] | null): string[] {
  if (target.configuration === null) return ['reference-fatal-source'];
  if (target.group === 'transpile') return ['transpile-runner'];
  const missing: string[] = [];
  const config = target.configuration.Config;
  for (const [rawKey, rawValue] of Object.entries(config)) {
    const key = rawKey.toLowerCase();
    const value = String(rawValue).trim().toLowerCase();
    if (key === 'filename') continue;
    if (key === 'target') {
      if (!SUPPORTED_TARGETS.has(value)) missing.push(`option:target=${value}`);
    } else if (key === 'module') {
      if (!SUPPORTED_MODULES.has(value)) missing.push(`option:module=${value}`);
    } else if (key === 'nolib' || key === 'noemit') {
      if (value !== 'true' && value !== 'false') missing.push(`option:${key}=${value}`);
    } else if (key === 'strict') {
      if (value !== 'true') missing.push(`option:strict=${value}`);
    } else if (key === 'lib') {
      // With @noLib the reference reports TS5053 and loads no library, which
      // tscs reproduces; without it the standard-library capability is missing.
    } else missing.push(`option:${key}`);
  }
  if (String(config.nolib ?? config.noLib ?? '').trim().toLowerCase() !== 'true') missing.push('standard-library');
  if (units !== null) {
    // Multi-source virtual files are now written to a private work directory.
    // Reject names that would escape it, silently alias, or require a foreign
    // filesystem convention. These are unsupported virtual filesystem inputs,
    // not executed failures that can be reclassified as SKIP.
    const names = new Set<string>();
    for (const u of units) {
      const ext = u.name.endsWith('.d.ts') ? '.d.ts' : extname(u.name).toLowerCase();
      if (ext !== '.ts' && ext !== '.d.ts') missing.push(`input:${ext || 'none'}`);
      if (u.name.length === 0 || u.name.includes('\\') || u.name.includes('\0') ||
          u.name.startsWith('/') || u.name.startsWith('-') || /^[A-Za-z]:/.test(u.name) ||
          u.name.split('/').some((segment) => segment === '' || segment === '.' || segment === '..') ||
          names.has(u.name)) missing.push('virtual-file-path');
      names.add(u.name);
    }
  }
  return [...new Set(missing)];
}

/** `name(Config)` baseline stem, as the pinned compiler runner names it. */
export function baselineStem(target: Target): string {
  const stem = basename(target.upstreamPath).replace(/\.[^.]+$/, '');
  const name = target.configuration?.Name ?? '';
  return name === '' ? stem : `${stem}(${name})`;
}

/** The summary lines of an `.errors.txt` baseline (before the first `====` block). */
export function baselineDiagnostics(errorsTxt: string | null): string[] {
  if (errorsTxt === null) return [];
  const text = errorsTxt.replace(/\r\n/g, '\n');
  const end = text.indexOf('\n\n');
  return (end < 0 ? text : text.slice(0, end)).split('\n').filter((l) => l.length > 0);
}

/** Output sections (`//// [x.js]`) of a `.js` baseline, keyed by file name. */
export function baselineOutputs(jsBaseline: string, inputs: Unit[]): Map<string, string> {
  const text = jsBaseline.replace(/\r\n/g, '\n');
  const outputs = new Map<string, string>();
  const headers = [...text.matchAll(/^\/\/\/\/ \[([^\]\n]+)\]( \/\/\/\/)?$/gm)];
  // The runner writes one section per input unit first, then the outputs.
  let inputsLeft = inputs.length;
  for (let i = 0; i < headers.length; i++) {
    const h = headers[i];
    if (h[2] !== undefined) continue;
    if (inputsLeft > 0) { inputsLeft--; continue; }
    const start = h.index! + h[0].length + 1;
    const end = i + 1 < headers.length ? headers[i + 1].index! : text.length;
    outputs.set(h[1], text.slice(start, end).replace(/\n*$/, ''));
  }
  return outputs;
}

export function compare(expectedDiagnostics: string[], actualDiagnostics: string[],
  expectedOutputs: Map<string, string>, actualOutputs: Map<string, string>): string[] {
  const reasons: string[] = [];
  if (expectedDiagnostics.join('\n') !== actualDiagnostics.join('\n')) {
    const missing = expectedDiagnostics.filter((d) => !actualDiagnostics.includes(d)).length;
    const extra = actualDiagnostics.filter((d) => !expectedDiagnostics.includes(d)).length;
    reasons.push(`diagnostics differ (${missing} missing, ${extra} unexpected)`);
  }
  // Line endings and trailing newlines are normalised on both sides.
  for (const [name, text] of expectedOutputs) {
    const actual = actualOutputs.get(name);
    if (actual === undefined) reasons.push(`emit missing: ${name}`);
    else if (actual.replace(/\r\n/g, '\n').replace(/\n*$/, '') !== text) reasons.push(`emit differs: ${name}`);
  }
  for (const name of actualOutputs.keys()) {
    if (!expectedOutputs.has(name)) reasons.push(`emit unexpected: ${name}`);
  }
  return reasons;
}

function tscsUnsupported(lines: string[]): string | null {
  const line = lines.find((l) => /error TSCS\d+:/.test(l));
  return line === undefined ? null : line.replace(/^.*?error (TSCS\d+): /, 'tscs $1: ');
}

export interface RunOptions {upstream: string; tscs: string; dotnet: string}

export function runTarget(target: Target, options: RunOptions): TargetResult {
  const result = (status: Status, reasons: string[]): TargetResult => ({
    id: target.id, upstreamPath: target.upstreamPath,
    configuration: target.configuration?.Name ?? '', status, reasons,
  });
  let units: Unit[] | null = null;
  let hasSymlinks = false;
  if (target.configuration !== null && target.group !== 'transpile') {
    const raw = readFileSync(join(options.upstream, target.upstreamPath));
    if (createHash('sha256').update(raw).digest('hex') !== target.rawSha256) {
      throw new Error(`${target.upstreamPath} does not match the registered pinned source`);
    }
    const source = raw.toString('utf8');
    hasSymlinks = source.split(/\r?\n/).some((line) => LINK.test(line));
    units = parseUnits(source, target.upstreamPath);
  }
  const missing = missingCapabilities(target, units);
  // parseUnits removes @link directives; do not execute such targets without
  // implementing their virtual symlink graph and reference paths.
  if (hasSymlinks) missing.push('virtual-symlink');
  if (missing.length > 0) return result('SKIP', missing);
  const suite = target.upstreamPath.includes('/cases/conformance/') ? 'conformance' : 'compiler';
  const baselineDir = join(options.upstream, 'tsc/testdata/baselines/reference', suite);
  const stem = baselineStem(target);
  const read = (p: string) => (existsSync(p) ? readFileSync(p, 'utf8') : null);
  // The pinned runner writes .types/.symbols for every configuration it runs;
  // without them the reference never ran this configuration (it skips some).
  if (!existsSync(join(baselineDir, `${stem}.types`)) && !existsSync(join(baselineDir, `${stem}.symbols`))) {
    return result('SKIP', ['no-reference-baseline']);
  }
  const expectedDiagnostics = baselineDiagnostics(read(join(baselineDir, `${stem}.errors.txt`)));
  const js = read(join(baselineDir, `${stem}.js`));
  const config = target.configuration!.Config;
  const noEmit = String(config.noemit ?? '').toLowerCase() === 'true';
  const work = mkdtempSync(join(tmpdir(), 'tscs-original-'));
  try {
    for (const u of units!) {
      mkdirSync(dirname(join(work, u.name)), {recursive: true});
      writeFileSync(join(work, u.name), u.content);
    }
    // The pinned TS7 default ES2015 output preserves import/export syntax.
    // tscs requires that effective module mode to be spelled explicitly;
    // this is an invocation adapter, not a different upstream test config.
    // Explicit ES2015/ES6 module mode is equivalent to this CLI's native
    // ES2015 module mode. Other module transforms are filtered above.
    const moduleOption = String(config.module ?? config.Module ?? '').trim().toLowerCase();
    const moduleMode = moduleOption === 'es6' || moduleOption === '' ? 'es2015' : moduleOption;
    const lib = config.lib === undefined ? [] : ['--lib', String(config.lib).trim()];
    const args = [options.tscs, noEmit ? '--noEmit' : '--emitJs', '--noLib', ...lib, '--target', 'es2015',
      '--module', moduleMode, ...units!.map((u) => u.name)];
    const run = spawnSync(options.dotnet, args, {cwd: work, encoding: 'utf8', timeout: 60_000});
    if (run.error) return result('FAIL', [`tscs did not run: ${run.error.message}`]);
    const lines = (run.stdout + run.stderr).split(/\r?\n/).filter((l) => l.length > 0);
    const unsupported = tscsUnsupported(lines);
    if (unsupported !== null) return result('FAIL', [unsupported]);
    if (run.status !== 0 && run.status !== 2) return result('FAIL', [`tscs exited with ${run.status ?? run.signal}`]);
    // The CLI exits 0 without diagnostics and 2 with them; anything else is not a clean comparison.
    if ((run.status === 2) !== (lines.length > 0)) return result('FAIL', [`tscs exit ${run.status} disagrees with ${lines.length} diagnostic line(s)`]);
    const actualOutputs = new Map<string, string>();
    const inputs = new Set(units!.map((u) => normalize(u.name).split(sep).join('/')));
    for (const entry of readdirSync(work, {recursive: true, withFileTypes: true})) {
      if (!entry.isFile()) continue;
      const name = relative(work, join(entry.parentPath, entry.name)).split(sep).join('/');
      if (!inputs.has(name)) actualOutputs.set(name, readFileSync(join(work, name), 'utf8'));
    }
    // No .js baseline (or noEmit) means the reference emitted nothing.
    const expectedOutputs = js === null || noEmit ? new Map<string, string>() : baselineOutputs(js, units!);
    const reasons = compare(expectedDiagnostics, lines, expectedOutputs, actualOutputs);
    return result(reasons.length === 0 ? 'PASS' : 'FAIL', reasons);
  } finally {
    rmSync(work, {recursive: true, force: true});
  }
}

export interface Report {
  upstreamCommit: string;
  totals: Record<Status, number>;
  /** Targets that need each capability (a target can count under several). */
  missingCapabilities: Record<string, number>;
  /** SKIP targets whose only missing capability is this one. */
  soleMissingCapability: Record<string, number>;
  failures: Record<string, number>;
  results: TargetResult[];
}

const sorted = (counts: Map<string, number>) =>
  Object.fromEntries([...counts].sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1)));

export function summarize(upstreamCommit: string, results: TargetResult[]): Report {
  const totals: Record<Status, number> = {PASS: 0, FAIL: 0, SKIP: 0};
  const all = new Map<string, number>();
  const sole = new Map<string, number>();
  const failures = new Map<string, number>();
  const bump = (m: Map<string, number>, k: string) => m.set(k, (m.get(k) ?? 0) + 1);
  for (const r of results) {
    totals[r.status]++;
    if (r.status === 'SKIP') {
      for (const reason of r.reasons) bump(all, reason.replace(/=.*$/, ''));
      if (r.reasons.length === 1) bump(sole, r.reasons[0].replace(/=.*$/, ''));
    } else if (r.status === 'FAIL') {
      for (const reason of r.reasons) bump(failures, reason.replace(/: .*$|\(.*\)$/, '').trim());
    }
  }
  return {upstreamCommit, totals, missingCapabilities: sorted(all), soleMissingCapability: sorted(sole),
    failures: sorted(failures), results};
}

export function headline(totals: Record<Status, number>): string {
  const n = (v: number) => v.toLocaleString('en-US');
  return `${n(totals.PASS)} PASS / ${n(totals.FAIL)} FAIL / ${n(totals.SKIP)} SKIP`;
}

export function readTargets(path: string): Target[] {
  return readFileSync(path, 'utf8').split('\n').filter((l) => l.trim().length > 0).map((l) => JSON.parse(l) as Target);
}

function argument(args: string[], name: string): string {
  const i = args.indexOf(name);
  if (i < 0 || i + 1 >= args.length) throw new Error(`missing ${name}`);
  return args[i + 1];
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const targets = readTargets(argument(args, '--targets'));
  const out = argument(args, '--out');
  if (existsSync(out)) throw new Error(`refusing to overwrite ${out}`);
  const lock = JSON.parse(readFileSync('upstream.lock.json', 'utf8')) as {commit: string};
  const options = {upstream: resolve(argument(args, '--upstream')), tscs: resolve(argument(args, '--tscs')),
    dotnet: process.env.DOTNET ?? 'dotnet'};
  if (targets.length !== 15523) throw new Error(`expected 15,523 registered targets, found ${targets.length}`);
  const report = summarize(lock.commit, targets.map((t) => runTarget(t, options)));
  writeFileSync(out, JSON.stringify(report, null, 2) + '\n', {flag: 'wx'});
  const measured = headline(report.totals);
  console.log(`Original upstream acceptance: ${measured}`);
  console.log('Sole missing capability (top 10):');
  for (const [k, v] of Object.entries(report.soleMissingCapability).slice(0, 10)) console.log(`  ${v}\t${k}`);
  console.log('Failures:');
  for (const [k, v] of Object.entries(report.failures)) console.log(`  ${v}\t${k}`);
  const docs = args.includes('--expect-headline') ? argument(args, '--expect-headline') : null;
  if (docs !== null) {
    const documented = headlineOf(readFileSync(docs, 'utf8'));
    if (documented !== measured) {
      console.error(`${docs} headline "${documented}" does not match the measured "${measured}"`);
      process.exit(1);
    }
  }
}
