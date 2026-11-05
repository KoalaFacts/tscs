import {test} from 'node:test';
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {mkdtempSync, mkdirSync, writeFileSync, rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {checkDocs, loadRepository, slug, anchorsOf, namedPaths, headlineOf, README_BUDGET, STATUS_BUDGET} from './check-docs.ts';

const HEAD = 'Original upstream acceptance: **0 PASS / 0 FAIL / 15,523 SKIP**';

function repo(files: Record<string, string>, extra: string[] = []) {
  const map = new Map(Object.entries(files));
  const present = new Set([...map.keys(), ...extra]);
  for (const p of [...present]) {
    const parts = p.split('/');
    for (let i = 1; i < parts.length; i++) present.add(parts.slice(0, i).join('/'));
  }
  return {files: map, exists: (p: string) => p === '' || present.has(p)};
}

function base(overrides: Record<string, string> = {}) {
  return {
    'README.md': `# tscs\n\n${HEAD}\n\n[Status](docs/STATUS.md) [Dev](docs/DEVELOPMENT.md#setup)\n`,
    'docs/STATUS.md': `# Status\n\n${HEAD}\n`,
    'docs/DEVELOPMENT.md': '# Dev\n\n## Setup\n\nRun `scripts/verify.sh`.\n',
    ...overrides,
  };
}

test('a consistent documentation set passes', () => {
  assert.deepEqual(checkDocs(repo(base(), ['scripts/verify.sh'])), []);
});

test('broken relative links fail', () => {
  const errors = checkDocs(repo(base({'docs/STATUS.md': `# Status\n\n${HEAD}\n\n[gone](progress/missing.md)\n`}), ['scripts/verify.sh']));
  assert.deepEqual(errors, ['docs/STATUS.md: broken link: progress/missing.md']);
});

test('missing anchors fail, including same-file anchors', () => {
  const errors = checkDocs(repo(base({'docs/STATUS.md': `# Status\n\n${HEAD}\n\n[a](DEVELOPMENT.md#nope) [b](#status) [c](#absent)\n`}), ['scripts/verify.sh']));
  assert.deepEqual(errors, ['docs/STATUS.md: missing anchor #nope in docs/DEVELOPMENT.md', 'docs/STATUS.md: missing anchor #absent in docs/STATUS.md']);
});

test('links leaving the repository fail', () => {
  const errors = checkDocs(repo(base({'docs/STATUS.md': `# Status\n\n${HEAD}\n\n[up](../../outside.md)\n`}), ['scripts/verify.sh']));
  assert.deepEqual(errors, ['docs/STATUS.md: link leaves the repository: ../../outside.md']);
});

test('a top-level doc not linked from the README fails', () => {
  const errors = checkDocs(repo(base({'docs/PORTING.md': '# Porting\n'}), ['scripts/verify.sh']));
  assert.deepEqual(errors, ['docs/PORTING.md: not linked from README.md']);
});

test('named repository paths that do not exist fail', () => {
  const errors = checkDocs(repo(base()));
  assert.deepEqual(errors, ['docs/DEVELOPMENT.md: named path does not exist: scripts/verify.sh']);
});

test('commands in code blocks are checked, but links inside code are ignored', () => {
  const dev = '# Dev\n\n## Setup\n\n```sh\nnode tools/gone.ts\n[x](nowhere.md)\n```\n';
  const errors = checkDocs(repo(base({'docs/DEVELOPMENT.md': dev})));
  assert.deepEqual(errors, ['docs/DEVELOPMENT.md: named path does not exist: tools/gone.ts']);
});

test('build outputs are validated through the project that produces them', () => {
  const dev = '# Dev\n\n## Setup\n\n```sh\ndotnet src/Tscs.Cli/bin/Release/net10.0/tscs.dll\ndotnet src/Gone/bin/Release/x.dll\n```\n';
  const errors = checkDocs(repo(base({'docs/DEVELOPMENT.md': dev}), ['src/Tscs.Cli/Tscs.Cli.csproj']));
  assert.deepEqual(errors, ['docs/DEVELOPMENT.md: named path does not exist: src/Gone']);
});

test('only files git tracks or would track are scanned', () => {
  const root = mkdtempSync(join(tmpdir(), 'tscs-check-docs-'));
  try {
    execFileSync('git', ['init', '-q'], {cwd: root});
    writeFileSync(join(root, '.gitignore'), '.worktrees/\n');
    mkdirSync(join(root, '.worktrees/other/docs/evidence'), {recursive: true});
    writeFileSync(join(root, '.worktrees/other/docs/evidence/receipt.md'), '[stale](missing.md)\n');
    writeFileSync(join(root, 'README.md'), '# tscs\n');
    mkdirSync(join(root, 'docs'));
    writeFileSync(join(root, 'docs/NEW.md'), '# New\n');
    assert.deepEqual([...loadRepository(root).files.keys()].sort(), ['README.md', 'docs/NEW.md']);
  } finally {
    rmSync(root, {recursive: true, force: true});
  }
});

test('kept evidence records are not rewritten to satisfy link checks', () => {
  const errors = checkDocs(repo(base({'docs/evidence/old.md': '[stale](missing.md)'}), ['scripts/verify.sh']));
  assert.deepEqual(errors, []);
});

test('size budgets are enforced', () => {
  const pad = 'x'.repeat(README_BUDGET);
  const errors = checkDocs(repo(base({
    'README.md': `${base()['README.md']}${pad}`,
    'docs/STATUS.md': `# Status\n\n${HEAD}\n${'y'.repeat(STATUS_BUDGET)}`,
  }), ['scripts/verify.sh']));
  assert.equal(errors.length, 2);
  assert.match(errors[0], /^README\.md is \d+ bytes; budget 10240$/);
  assert.match(errors[1], /^docs\/STATUS\.md is \d+ bytes; budget 25600$/);
});

test('disagreeing or missing headline counts fail', () => {
  let errors = checkDocs(repo(base({'docs/STATUS.md': '# Status\n\nOriginal upstream acceptance: 1 PASS / 0 FAIL / 15,522 SKIP\n'}), ['scripts/verify.sh']));
  assert.deepEqual(errors, ['headline counts disagree: README.md "0 PASS / 0 FAIL / 15,523 SKIP" vs docs/STATUS.md "1 PASS / 0 FAIL / 15,522 SKIP"']);
  errors = checkDocs(repo(base({'README.md': '# tscs\n\n[Status](docs/STATUS.md) [Dev](docs/DEVELOPMENT.md)\n'}), ['scripts/verify.sh']));
  assert.deepEqual(errors, ['README.md has no "Original upstream acceptance: N PASS / N FAIL / N SKIP" headline']);
});

test('slugs and duplicate headings follow GitHub anchors', () => {
  assert.equal(slug('Build, test & run (Linux x64)'), 'build-test--run-linux-x64');
  assert.equal(slug('`check-docs` rules'), 'check-docs-rules');
  assert.deepEqual([...anchorsOf('# A\n## A\n```\n# not a heading\n```\n## B_c\n')], ['a', 'a-1', 'b_c']);
});

test('named paths ignore globs, placeholders and non-repository tokens', () => {
  assert.deepEqual(namedPaths('Use `tools/*.test.ts`, `src/<area>/x.cs`, `docs/STATUS.md`, `npm test` and `./scripts/verify.sh`.'), ['docs/STATUS.md', 'scripts/verify.sh']);
  assert.equal(headlineOf('nothing'), null);
});

test('the repository documentation passes', () => {
  assert.deepEqual(checkDocs(loadRepository(process.cwd())), []);
});
