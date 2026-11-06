import assert from 'node:assert/strict';
import {spawnSync, type SpawnSyncReturns} from 'node:child_process';
import {resolve} from 'node:path';

// One way for the tools/verify-*-emit.ts checks to run the built tscs CLI,
// instead of a copy of the same spawn-and-assert helper in every script.

/** The Release CLI that the gate builds before running the emit checks. */
export const tscsDll = resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');

/** The caller's environment with .NET and test-platform telemetry off. */
export const quietEnv: NodeJS.ProcessEnv = {
  ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', TESTINGPLATFORM_TELEMETRY_OPTOUT: '1',
};

/**
 * Runs `dotnet tscs.dll ...args` in `cwd` (DOTNET overrides the host) and
 * fails when the process could not start, crashed or timed out. The exit
 * status and output are returned for the caller to assert on.
 */
export function runTscs(cwd: string, args: readonly string[], timeout = 30_000): SpawnSyncReturns<string> {
  const result = spawnSync(process.env.DOTNET ?? 'dotnet', [tscsDll, ...args],
    {cwd, encoding: 'utf8', timeout, maxBuffer: 4 * 1024 * 1024, env: quietEnv});
  assert.equal(result.error, undefined, String(result.error));
  assert.equal(result.signal, null, 'compiler crashed or timed out');
  return result;
}
