import assert from 'node:assert/strict';
import {chmodSync, mkdtempSync, rmSync, writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {test} from 'node:test';
import {quietEnv, runTscs, tscsDll} from './tscs-process.ts';

// A stand-in for `dotnet`: echoes its arguments, cwd and telemetry settings,
// and crashes, hangs or fails on request.
const FAKE = `#!${process.execPath}
const args = process.argv.slice(2);
if (args.includes('crash')) process.kill(process.pid, 'SIGKILL');
if (args.includes('hang')) setInterval(() => {}, 1000);
else {
  console.log(JSON.stringify({args, cwd: process.cwd(), dotnet: process.env.DOTNET_CLI_TELEMETRY_OPTOUT, platform: process.env.TESTINGPLATFORM_TELEMETRY_OPTOUT}));
  process.exitCode = args.includes('fail') ? 2 : 0;
}
`;

function withFake(body: (folder: string) => void) {
  const folder = mkdtempSync(join(tmpdir(), 'tscs-process-test-'));
  const fake = join(folder, 'fake-dotnet.cjs');
  writeFileSync(fake, FAKE);
  chmodSync(fake, 0o755);
  const saved = process.env.DOTNET;
  process.env.DOTNET = fake;
  try { body(folder); } finally {
    if (saved === undefined) delete process.env.DOTNET; else process.env.DOTNET = saved;
    rmSync(folder, {recursive: true, force: true});
  }
}

test('runs the Release CLI in the given folder with telemetry off and returns its status', () => withFake((folder) => {
  const ok = runTscs(folder, ['--noEmit', 'a.ts']);
  assert.equal(ok.status, 0);
  const seen = JSON.parse(ok.stdout);
  assert.deepEqual(seen.args, [tscsDll, '--noEmit', 'a.ts']);
  assert.equal(seen.cwd, folder);
  assert.deepEqual([seen.dotnet, seen.platform], ['1', '1']);
  assert.equal(runTscs(folder, ['fail']).status, 2, 'a diagnostic exit is returned, not thrown');
  assert.equal(quietEnv.DOTNET_CLI_TELEMETRY_OPTOUT, '1');
}));

test('a crash, a timeout or a missing host fails instead of returning', () => withFake((folder) => {
  assert.throws(() => runTscs(folder, ['crash']), /compiler crashed or timed out/);
  assert.throws(() => runTscs(folder, ['hang'], 500), /ETIMEDOUT/);
  process.env.DOTNET = join(folder, 'missing-dotnet');
  assert.throws(() => runTscs(folder, []), /ENOENT/);
}));
