// Documentation consistency checker. Run from the repository root:
//   node tools/check-docs.ts
// Fails on broken relative links/anchors, top-level docs missing from the
// README index, named repository paths that do not exist, README/STATUS size
// budgets, and disagreeing headline acceptance counts.
import {readFileSync, readdirSync, statSync, existsSync} from 'node:fs';
import {join, dirname, normalize, relative, resolve, sep} from 'node:path';
import {fileURLToPath} from 'node:url';

export const README_BUDGET = 10 * 1024;
export const STATUS_BUDGET = 25 * 1024;
const SKIP_DIRS = new Set(['.git', 'node_modules', 'bin', 'obj', '.artifacts']);
// docs/evidence holds kept historical records whose contents are never rewritten.
const KEPT_RECORD_PREFIX = 'docs/evidence/';
const PATH_ROOTS = ['src/', 'tests/', 'tools/', 'scripts/', 'docs/', 'fixtures/', '.github/'];
// Named-path checks cover the curated pages; inventory pages under docs/testing
// quote upstream TypeScript paths that are not part of this repository.
const CURATED = /^(README\.md|AGENTS\.md|docs\/[^/]+\.md)$/;
const ROOT_FILES = ['README.md', 'AGENTS.md', 'LICENSE', 'NOTICE', 'global.json', 'upstream.lock.json', 'Tscs.slnx', 'Directory.Build.props'];

export interface DocsInput {
  /** Markdown files keyed by repository-relative POSIX path. */
  files: Map<string, string>;
  /** Whether a repository-relative POSIX path (file or directory) exists. */
  exists: (path: string) => boolean;
}

/** GitHub-compatible heading anchor slug. */
export function slug(heading: string): string {
  return heading
    .trim()
    .toLowerCase()
    .replace(/<[^>]+>/g, '')
    .replace(/[^\p{L}\p{N}\s_-]/gu, '')
    .replace(/\s/g, '-');
}

/** Removes fenced code blocks, keeping line structure. */
function stripFences(text: string): {prose: string; code: string} {
  const prose: string[] = [];
  const code: string[] = [];
  let fence: string | null = null;
  for (const line of text.split('\n')) {
    const m = /^\s*(```+|~~~+)/.exec(line);
    if (fence === null && m) { fence = m[1][0]; prose.push(''); continue; }
    if (fence !== null && m && m[1][0] === fence) { fence = null; prose.push(''); continue; }
    if (fence !== null) { code.push(line); prose.push(''); } else prose.push(line);
  }
  return {prose: prose.join('\n'), code: code.join('\n')};
}

export function anchorsOf(text: string): Set<string> {
  const anchors = new Set<string>();
  const counts = new Map<string, number>();
  for (const line of stripFences(text).prose.split('\n')) {
    const m = /^#{1,6}\s+(.*?)\s*#*\s*$/.exec(line);
    if (!m) continue;
    const base = slug(m[1].replace(/\[([^\]]*)\]\([^)]*\)/g, '$1'));
    const n = counts.get(base) ?? 0;
    counts.set(base, n + 1);
    anchors.add(n === 0 ? base : `${base}-${n}`);
  }
  return anchors;
}

function linksOf(text: string): string[] {
  const prose = stripFences(text).prose.replace(/`[^`\n]*`/g, '');
  const out: string[] = [];
  for (const m of prose.matchAll(/!?\[[^\]\n]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g)) out.push(m[1]);
  return out;
}

function posixJoin(fromFile: string, target: string): string {
  const joined = normalize(join(dirname(fromFile), target)).split(sep).join('/');
  return joined.replace(/\/$/, '');
}

/** Repository paths named in inline code or code blocks. */
export function namedPaths(text: string): string[] {
  const {prose, code} = stripFences(text);
  const tokens: string[] = [];
  for (const m of prose.matchAll(/`([^`\n]+)`/g)) tokens.push(...m[1].split(/\s+/));
  for (const line of code.split('\n')) tokens.push(...line.split(/\s+/));
  const paths = new Set<string>();
  for (let token of tokens) {
    token = token.replace(/^["'(]+|["'),;:]+$/g, '').replace(/^\.\//, '');
    if (!token || /[*?{}$<>|]/.test(token) || token.includes('..')) continue;
    if (ROOT_FILES.includes(token) || (PATH_ROOTS.some((r) => token.startsWith(r)) && /^[\w./@+-]+$/.test(token))) {
      paths.add(token.replace(/\/$/, ''));
    }
  }
  return [...paths];
}

export function headlineOf(text: string): string | null {
  const m = /Original upstream acceptance:\s*\**\s*([\d,]+) PASS \/ ([\d,]+) FAIL \/ ([\d,]+) SKIP/.exec(text);
  return m ? `${m[1]} PASS / ${m[2]} FAIL / ${m[3]} SKIP` : null;
}

export function checkDocs(input: DocsInput): string[] {
  const errors: string[] = [];
  const anchorCache = new Map<string, Set<string>>();
  const anchors = (path: string) => {
    let a = anchorCache.get(path);
    if (!a) { a = anchorsOf(input.files.get(path) ?? ''); anchorCache.set(path, a); }
    return a;
  };
  for (const [path, text] of input.files) {
    if (path.startsWith(KEPT_RECORD_PREFIX)) continue;
    for (const link of linksOf(text)) {
      if (/^[a-z][a-z0-9+.-]*:/i.test(link)) continue;
      const hash = link.indexOf('#');
      const target = hash >= 0 ? link.slice(0, hash) : link;
      const anchor = hash >= 0 ? decodeURIComponent(link.slice(hash + 1)) : '';
      const resolved = target === '' ? path : posixJoin(path, decodeURIComponent(target));
      if (resolved.startsWith('../')) { errors.push(`${path}: link leaves the repository: ${link}`); continue; }
      if (!input.exists(resolved)) { errors.push(`${path}: broken link: ${link}`); continue; }
      if (anchor && resolved.endsWith('.md') && !anchors(resolved).has(anchor)) {
        errors.push(`${path}: missing anchor #${anchor} in ${resolved}`);
      }
    }
    if (!CURATED.test(path)) continue;
    for (const named of namedPaths(text)) {
      if (!input.exists(named)) errors.push(`${path}: named path does not exist: ${named}`);
    }
  }
  const readme = input.files.get('README.md');
  if (readme === undefined) {
    errors.push('README.md is missing');
    return errors;
  }
  const indexed = new Set(linksOf(readme).map((l) => posixJoin('README.md', decodeURIComponent(l.split('#')[0] || 'README.md'))));
  for (const path of input.files.keys()) {
    if (/^docs\/[^/]+\.md$/.test(path) && !indexed.has(path)) errors.push(`${path}: not linked from README.md`);
  }
  const size = (p: string) => Buffer.byteLength(input.files.get(p) ?? '', 'utf8');
  if (size('README.md') > README_BUDGET) errors.push(`README.md is ${size('README.md')} bytes; budget ${README_BUDGET}`);
  const status = input.files.get('docs/STATUS.md');
  if (status === undefined) errors.push('docs/STATUS.md is missing');
  else {
    if (size('docs/STATUS.md') > STATUS_BUDGET) errors.push(`docs/STATUS.md is ${size('docs/STATUS.md')} bytes; budget ${STATUS_BUDGET}`);
    const a = headlineOf(readme);
    const b = headlineOf(status);
    if (a === null) errors.push('README.md has no "Original upstream acceptance: N PASS / N FAIL / N SKIP" headline');
    if (b === null) errors.push('docs/STATUS.md has no "Original upstream acceptance: N PASS / N FAIL / N SKIP" headline');
    if (a !== null && b !== null && a !== b) errors.push(`headline counts disagree: README.md "${a}" vs docs/STATUS.md "${b}"`);
  }
  return errors;
}

export function loadRepository(root: string): DocsInput {
  const files = new Map<string, string>();
  const walk = (dir: string) => {
    for (const name of readdirSync(dir)) {
      if (SKIP_DIRS.has(name)) continue;
      const full = join(dir, name);
      if (statSync(full).isDirectory()) walk(full);
      else if (name.endsWith('.md')) files.set(relative(root, full).split(sep).join('/'), readFileSync(full, 'utf8'));
    }
  };
  walk(root);
  return {files, exists: (p) => p === '' || existsSync(join(root, p))};
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const errors = checkDocs(loadRepository(process.cwd()));
  for (const e of errors) console.error(e);
  if (errors.length > 0) {
    console.error(`check-docs: ${errors.length} problem(s)`);
    process.exit(1);
  }
  console.log('check-docs: ok');
}
