import test from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtempSync, rmSync, writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {fileURLToPath} from 'node:url';

const root=fileURLToPath(new URL('../', import.meta.url));

test('a supported 400000-addition chain cannot overflow the C# checker stack', () => {
  const directory=mkdtempSync(join(tmpdir(), 'tscs-deep-addition-'));
  try {
    const input=join(directory, 'deep.ts');
    const source='let n = '+'1+'.repeat(400000)+'1;';
    assert.equal(source.length, 800010); // below the existing 1,000,000-code-unit limit
    writeFileSync(input, source);
    // Run in a child process: a pre-fix StackOverflow must fail the test,
    // never terminate the Node test runner or the xUnit/MTP coordinator.
    const result=spawnSync(process.env.DOTNET??'dotnet', [
      join(root, 'src/Tscs.Cli/bin/Release/net10.0/tscs.dll'),
      '--json', '--noEmit', '--noLib', '--target', 'es2015',
      input, join(root, 'fixtures/minimal.d.ts')
    ], {
      cwd:root, encoding:'utf8', timeout:90000, maxBuffer:2*1024*1024,
      env:{...process.env, DOTNET_CLI_TELEMETRY_OPTOUT:'1', TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'}
    });
    assert.equal(result.error, undefined, String(result.error));
    assert.equal(result.signal, null, String(result.signal));
    assert.equal(result.status, 0, 'C# compiler child failed: '+String(result.stderr).slice(-1000));
    const output=JSON.parse(result.stdout);
    assert.equal(output.exitCode, 0);
    assert.deepEqual(output.diagnostics, []);
  } finally {
    rmSync(directory, {recursive:true, force:true});
  }
});
