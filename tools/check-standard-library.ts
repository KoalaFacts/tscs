// Standard library syntax check (roadmap A2.1).
//
// Parses every pinned bundled lib file (<upstream>/tsc/internal/bundled/libs)
// with tscs's DeclarationParser through the development fixture harness and
// reports which files parse with zero diagnostics. This is syntax only: the
// files are not bound or checked, the syntax trees are not compared with the
// reference parser, and programs still use --noLib. Usage (repository root):
//
//   node tools/check-standard-library.ts --upstream <TypeScript checkout> \
//     --harness tools/Tscs.FixtureHarness/bin/Release/net10.0/Tscs.FixtureHarness.dll \
//     --out <report.json>
//
// Exits 1 when any file reachable through `/// <reference lib>` from the
// required roots (lib.es5.d.ts and lib.es2015.d.ts) has a diagnostic.
import {spawnSync} from 'node:child_process';
import {existsSync, readFileSync, readdirSync, writeFileSync} from 'node:fs';
import {join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

export const REQUIRED_ROOTS = ['lib.es5.d.ts', 'lib.es2015.d.ts'];

export interface Diagnostic {code: string; start: number; message: string}
export interface FileResult {file: string; statements: number; libReferences: string[]; diagnostics: Diagnostic[]}
export interface Report {
  libs: number;
  clean: number;
  required: {file: string; clean: boolean}[];
  firstDiagnostics: Record<string, string[]>;
  files: FileResult[];
}
export interface HarnessOptions {dotnet: string; harness: string}

export function libFiles(dir: string): string[] {
  return readdirSync(dir).filter((f) => f.startsWith('lib.') && f.endsWith('.d.ts')).sort();
}

export function parseLibs(dir: string, files: string[], options: HarnessOptions): FileResult[] {
  const input = files.map((f) => JSON.stringify({id: f, profile: 'declaration-syntax', fileName: f,
    input: readFileSync(join(dir, f)).toString('base64')})).join('\n') + '\n';
  const run = spawnSync(options.dotnet, [options.harness], {input, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024});
  if (run.status !== 0) throw new Error(`harness exited with ${run.status}: ${run.stderr}`);
  const rows = run.stdout.split('\n').filter((l) => l.trim().length > 0)
    .map((l) => JSON.parse(l) as {id: string; expected: {statements: number;
      references: {name: string; value: string}[]; diagnostics: Diagnostic[]}});
  if (rows.length !== files.length || rows.some((r, i) => r.id !== files[i])) throw new Error('harness returned mismatched rows');
  return rows.map((r) => ({file: r.id, statements: r.expected.statements,
    libReferences: r.expected.references.filter((x) => x.name === 'lib').map((x) => `lib.${x.value.toLowerCase()}.d.ts`),
    diagnostics: r.expected.diagnostics}));
}

// Files reachable from the roots through lib reference directives.
export function requiredClosure(results: FileResult[], roots: string[]): string[] {
  const byName = new Map(results.map((r) => [r.file, r]));
  const seen = new Set<string>();
  const pending = [...roots];
  while (pending.length > 0) {
    const name = pending.pop()!;
    if (seen.has(name)) continue;
    const result = byName.get(name);
    if (!result) throw new Error(`required lib ${name} is not in the pinned libs`);
    seen.add(name);
    pending.push(...result.libReferences);
  }
  return [...seen].sort();
}

export function summarize(results: FileResult[], roots: string[]): Report {
  const firstDiagnostics: Record<string, string[]> = {};
  for (const r of results) {
    if (r.diagnostics.length === 0) continue;
    const key = `${r.diagnostics[0].code} ${r.diagnostics[0].message}`;
    (firstDiagnostics[key] ??= []).push(r.file);
  }
  const clean = (name: string) => results.find((r) => r.file === name)!.diagnostics.length === 0;
  return {
    libs: results.length,
    clean: results.filter((r) => r.diagnostics.length === 0).length,
    required: requiredClosure(results, roots).map((file) => ({file, clean: clean(file)})),
    firstDiagnostics: Object.fromEntries(Object.entries(firstDiagnostics).sort((a, b) => b[1].length - a[1].length)),
    files: results,
  };
}

function argument(args: string[], name: string): string {
  const i = args.indexOf(name);
  if (i < 0 || i + 1 >= args.length) throw new Error(`missing ${name}`);
  return args[i + 1];
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const dir = join(resolve(argument(args, '--upstream')), 'tsc/internal/bundled/libs');
  const out = argument(args, '--out');
  if (existsSync(out)) throw new Error(`refusing to overwrite ${out}`);
  const options = {dotnet: process.env.DOTNET ?? 'dotnet', harness: resolve(argument(args, '--harness'))};
  const files = libFiles(dir);
  const report = summarize(parseLibs(dir, files, options), REQUIRED_ROOTS);
  writeFileSync(out, JSON.stringify(report, null, 2) + '\n', {flag: 'wx'});
  console.log(`Standard library syntax: ${report.clean} of ${report.libs} pinned lib files parse with zero diagnostics`);
  for (const [k, v] of Object.entries(report.firstDiagnostics)) console.log(`  ${v.length}\t${k}\t${v.join(', ')}`);
  const failing = report.required.filter((r) => !r.clean);
  console.log(`Required (es5 and es2015 closure): ${report.required.length - failing.length} of ${report.required.length} clean`);
  if (failing.length > 0) {
    console.error(`required lib files with diagnostics: ${failing.map((r) => r.file).join(', ')}`);
    process.exit(1);
  }
}
