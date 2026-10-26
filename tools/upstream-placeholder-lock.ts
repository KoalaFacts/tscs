// The external generated configuration-placeholder project has one NuGet
// direct dependency: exactly the xUnit 4/MTP 2 package used by the real suite.
// Copy its verified package graph, never copy project references or regenerate
// content hashes from an untrusted service response.
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';

function requireValid(value: unknown, message: string): asserts value {
  if (!value) throw new Error(message);
}

export const pinnedSuiteLockSha256 = '66df11cc3441bf528b1769abdc3dcc5db213d445e4872bc91fa8aee6b8f0f4a0';
export function projectPlaceholderLock(source: any) {
  requireValid(source && source.version === 1 && source.dependencies &&
    Object.keys(source.dependencies).length === 1 && source.dependencies['net10.0'],
    'Expected only net10.0 in the pinned source lock');
  const original = source.dependencies['net10.0'];
  requireValid(typeof original === 'object' && !Array.isArray(original),
    'Missing net10.0 package graph');
  const keys = Object.keys(original);
  const direct = keys.filter(key => original[key]?.type === 'Direct');
  requireValid(direct.length === 1 && direct[0] === 'xunit.v3.mtp-v2',
    'Expected exactly one direct xunit.v3.mtp-v2 dependency');
  requireValid(original['xunit.v3.mtp-v2'].requested === '[4.0.2, )' &&
    original['xunit.v3.mtp-v2'].resolved === '4.0.2',
    'Expected pinned xunit.v3.mtp-v2 4.0.2');
  const result: Record<string, unknown> = {};
  for (const key of keys) {
    const item = original[key];
    requireValid(item && typeof item === 'object', 'Invalid NuGet package entry: ' + key);
    if (item.type === 'Project') continue;
    requireValid(['Direct','Transitive'].includes(item.type) &&
      typeof item.resolved === 'string' && item.resolved.length > 0 &&
      typeof item.contentHash === 'string' && item.contentHash.length > 0,
      'Missing pinned package contentHash/version: ' + key);
    result[key] = item;
  }
  requireValid(Object.keys(result).length >= 2, 'No complete pinned dependency graph');
  return { version: 1, dependencies: { 'net10.0': result } };
}

export function verifyPlaceholderLock(source: any, candidate: any) {
  const expected = projectPlaceholderLock(source);
  requireValid(JSON.stringify(expected) === JSON.stringify(candidate),
    'Standalone placeholder lock mismatch: package/version/hash changed');
  return { packages: Object.keys(expected.dependencies['net10.0']).length,
    direct: 'xunit.v3.mtp-v2@4.0.2', verified: true };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    requireValid(process.argv.length === 4,
      'Usage: node tools/upstream-placeholder-lock.ts EXISTING_SUITE_LOCK REVIEWED_PLACEHOLDER_LOCK');
    const sourceBytes = readFileSync(resolve(process.argv[2]));
    requireValid(createHash('sha256').update(sourceBytes).digest('hex') === pinnedSuiteLockSha256,
      'Pinned compiler-suite NuGet lock changed; require separate version/hash review');
    const original = JSON.parse(sourceBytes.toString('utf8'));
    const candidate = JSON.parse(readFileSync(resolve(process.argv[3]), 'utf8'));
    console.log(JSON.stringify(verifyPlaceholderLock(original, candidate)));
  } catch (error) {
    console.error((error as Error).message);
    process.exitCode = 1;
  }
}
