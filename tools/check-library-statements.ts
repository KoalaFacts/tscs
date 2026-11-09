// Standard library statement check (roadmap A2.3 progress measure).
//
// Splits one pinned bundled lib file (default lib.es5.d.ts) into its
// top-level statements and checks each statement alone as a declaration file
// through the real tscs CLI (`--noEmit --noLib --target es2015`, with a
// one-line .ts program and fixtures/minimal.d.ts). Prints how many statements
// check without diagnostics and a histogram of the first diagnostic of every
// failing statement, so A2.3 progress is measured against the pinned lib
// rather than an unpinned copy. A statement checked alone may need names other
// statements declare (TS2304 then counts as a failure): this is a progress
// measure, not loading the library. Usage (repository root):
//
//   node tools/check-library-statements.ts --upstream <TypeScript checkout> \
//     --tscs src/Tscs.Cli/bin/Release/net10.0/tscs.dll [--lib lib.es5.d.ts] [--out <report.json>]
//
// Exits 1 when the lib file is missing or contains no statements; failing
// statements are the measurement, not an error.
import {spawnSync} from 'node:child_process';
import {existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

// Declarations whose body closes the statement at its top-level `}`. Every
// other statement (var, function, type alias) ends at a top-level `;`.
const BLOCK_DECLARATIONS = new Set(['interface', 'namespace', 'module', 'class', 'enum', 'global', 'abstract']);
const STATEMENT_STARTS = /^\s*(?:export|declare|interface|type|namespace|module|var|let|const|function|class|enum|abstract|import)\b/;
const MODIFIERS = new Set(['export', 'declare', 'default', 'const']);

function head(statement: string): string | undefined {
  for (const word of statement.match(/[A-Za-z_$][\w$]*/g) ?? []) {
    if (!MODIFIERS.has(word)) return word;
  }
  return undefined;
}

/**
 * Top-level statements of a declaration file, with comments removed and
 * `/// <reference>` directives dropped. Throws on an unterminated comment,
 * string or bracket so a malformed input is never silently counted.
 */
export function splitTopLevelStatements(text: string): string[] {
  const statements: string[] = [];
  let current = '';
  let depth = 0;
  let i = 0;
  const flush = () => {
    const trimmed = current.trim();
    if (trimmed) statements.push(trimmed);
    current = '';
  };
  while (i < text.length) {
    const c = text[i];
    if (c === '/' && text[i + 1] === '/') {
      const end = text.indexOf('\n', i);
      i = end < 0 ? text.length : end;
      continue;
    }
    if (c === '/' && text[i + 1] === '*') {
      const end = text.indexOf('*/', i + 2);
      if (end < 0) throw new Error('unterminated block comment');
      i = end + 2;
      current += ' ';
      continue;
    }
    if (c === '"' || c === "'" || c === '`') {
      let j = i + 1;
      while (j < text.length && text[j] !== c) j += text[j] === '\\' ? 2 : 1;
      if (j >= text.length) throw new Error('unterminated string literal');
      current += text.slice(i, j + 1);
      i = j + 1;
      continue;
    }
    // A new declaration on a later line at depth 0 ends a statement written
    // without its semicolon (automatic semicolon insertion).
    if (c === '\n' && depth === 0 && current.trim() && STATEMENT_STARTS.test(text.slice(i + 1, i + 64))) flush();
    current += c;
    i++;
    if (c === '{' || c === '(' || c === '[') depth++;
    else if (c === '}' || c === ')' || c === ']') {
      depth--;
      if (depth < 0) throw new Error(`unbalanced '${c}'`);
      if (depth === 0 && c === '}' && BLOCK_DECLARATIONS.has(head(current) ?? '')) {
        while (text[i] === ' ' || text[i] === '\t') i++;
        if (text[i] === ';') i++;
        flush();
      }
    } else if (c === ';' && depth === 0) flush();
  }
  if (depth !== 0) throw new Error('unterminated bracket');
  flush();
  return statements;
}

export interface StatementResult {index: number; head: string; passed: boolean; firstDiagnostic: string}
export interface Report {lib: string; statements: number; passed: number; histogram: [string, number][]; results: StatementResult[]}

/** The first `error ...` line tscs printed, without its file position. */
export function firstDiagnostic(output: string): string {
  const line = output.split('\n').find((l) => /error [A-Z]+\d+:/.test(l)) ?? output.trim().split('\n')[0] ?? '';
  return line.replace(/^.*?error /, '').trim();
}

export function checkStatements(lib: string, statements: string[], tscs: string, minimal: string, dotnet = 'dotnet'): Report {
  const dir = mkdtempSync(join(tmpdir(), 'tscs-lib-statements-'));
  try {
    const program = join(dir, 'program.ts');
    const declaration = join(dir, 'statement.d.ts');
    writeFileSync(program, 'let program: number = 1;\n');
    const results = statements.map((statement, index): StatementResult => {
      writeFileSync(declaration, `${statement}\n`);
      const run = spawnSync(dotnet, [tscs, '--noEmit', '--noLib', '--target', 'es2015', program, declaration, minimal],
        {encoding: 'utf8', timeout: 60_000});
      if (run.error) throw run.error;
      const passed = run.status === 0;
      return {index, head: head(statement) ?? '', passed, firstDiagnostic: passed ? '' : firstDiagnostic(`${run.stdout}\n${run.stderr}`)};
    });
    const counts = new Map<string, number>();
    for (const r of results) if (!r.passed) counts.set(r.firstDiagnostic, (counts.get(r.firstDiagnostic) ?? 0) + 1);
    const histogram = [...counts].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
    return {lib, statements: results.length, passed: results.filter((r) => r.passed).length, histogram, results};
  } finally {
    rmSync(dir, {recursive: true, force: true});
  }
}

function argument(args: string[], name: string, fallback?: string): string {
  const index = args.indexOf(name);
  if (index >= 0 && args[index + 1]) return args[index + 1];
  if (fallback !== undefined) return fallback;
  throw new Error(`missing ${name}`);
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  const lib = argument(args, '--lib', 'lib.es5.d.ts');
  const file = join(resolve(argument(args, '--upstream')), 'tsc/internal/bundled/libs', lib);
  if (!existsSync(file)) throw new Error(`pinned lib ${file} does not exist`);
  const statements = splitTopLevelStatements(readFileSync(file, 'utf8'));
  if (statements.length === 0) throw new Error(`${lib} has no top-level statements`);
  const minimal = resolve(fileURLToPath(new URL('../fixtures/minimal.d.ts', import.meta.url)));
  const report = checkStatements(lib, statements, resolve(argument(args, '--tscs')), minimal, process.env.DOTNET ?? 'dotnet');
  const out = args.includes('--out') ? argument(args, '--out') : undefined;
  if (out) writeFileSync(out, `${JSON.stringify(report, null, 2)}\n`);
  console.log(`${lib}: ${report.passed} of ${report.statements} top-level statements check alone`);
  for (const [message, count] of report.histogram.slice(0, 10)) console.log(`${String(count).padStart(5)}  ${message}`);
}
