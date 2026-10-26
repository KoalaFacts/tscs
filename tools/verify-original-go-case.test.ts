import test from 'node:test';
import assert from 'node:assert/strict';
import {inspectPinnedEmptyFileGoRun, requiredSubtests} from './verify-original-go-case.ts';

const pkg = 'github.com/microsoft/TypeScript/tsc/internal/testrunner';
const root = 'TestLocal';
const target = `${root}/emptyFile.ts`;
const events = (options = {}) => {
  const {omit = '', skip = '', fail = '', add = '', packageFail = false} = options;
  const names = [root, target, ...requiredSubtests.map(subtest => `${target}/${subtest}`)];
  const rows = [{Action: 'start', Package: pkg}];
  for (const name of names) {
    if (name === omit) continue;
    rows.push({Action: 'run', Package: pkg, Test: name});
    rows.push({Action: name === skip ? 'skip' : name === fail ? 'fail' : 'pass', Package: pkg, Test: name});
  }
  if (add) {
    rows.push({Action: 'run', Package: pkg, Test: add});
    rows.push({Action: 'pass', Package: pkg, Test: add});
  }
  rows.push({Action: packageFail ? 'fail' : 'pass', Package: pkg});
  return rows.map(row => JSON.stringify(row)).join('\n') + '\n';
};

test('accepts one original Go TestLocal target and every expected nested assertion', () => {
  const result = inspectPinnedEmptyFileGoRun(events(), 0);
  assert.equal(result.originalReference, 'PASS');
  assert.equal(result.productAcceptance, 'NOT_RUN');
  assert.equal(result.releaseEligible, false);
  assert.equal(result.target, target);
  assert.equal(result.expectedSubtests.length, requiredSubtests.length);
  assert.equal(result.executedTestCount, requiredSubtests.length + 2);
});
test('rejects a zero-selection Go success', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(JSON.stringify({Action:'pass',Package:pkg})+'\n', 0), /missing|Missing/i);
});
test('rejects a reference test that was skipped', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events({skip:target}), 0), /skip|pass/i);
});
test('rejects a skipped assertion subtest even when the target itself passes', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events({skip:`${target}/output`}), 0), /skip|pass/i);
});
test('rejects a failed Go diagnostic assertion even when the parent passes', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events({fail:`${target}/error`}), 0), /fail|pass/i);
});
test('rejects any missing original harness assertion', () => {
  for (const name of requiredSubtests) {
    assert.throws(() => inspectPinnedEmptyFileGoRun(events({omit:`${target}/${name}`}), 0), /missing|Missing/i, name);
  }
});
test('rejects unexpected selected tests instead of silently widening scope', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events({add:'TestLocal/other.ts'}), 0), /Unexpected|unexpected/i);
});
test('rejects missing successful package exit and nonzero Go process exit', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events({packageFail:true}), 0), /package|pass|fail/i);
  assert.throws(() => inspectPinnedEmptyFileGoRun(events(), 1), /exit/i);
});
test('rejects duplicated Go run events rather than double counting', () => {
  const lines = events().trimEnd().split('\n');
  const duplicate = lines.find(line => JSON.parse(line).Test === target && JSON.parse(line).Action === 'run');
  assert.throws(() => inspectPinnedEmptyFileGoRun([...lines.slice(0,-1),duplicate,lines.at(-1)].join('\n')+'\n', 0), /duplicate|Duplicate/i);
});
test('rejects truncated and malformed JSONL without interpreting partial output as a pass', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events() + '{"Action":', 0), /JSON|malformed/i);
  assert.throws(() => inspectPinnedEmptyFileGoRun(events().trimEnd().split('\n').slice(0,-1).join('\n')+'\n', 0), /package|pass/i);
});
test('rejects an invalid Go package and missing parent TestLocal', () => {
  assert.throws(() => inspectPinnedEmptyFileGoRun(events().replaceAll(pkg,'malicious/example'),0), /package|Unexpected/i);
  assert.throws(() => inspectPinnedEmptyFileGoRun(events({omit:root}),0), /missing|Missing/i);
});
