import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {test} from 'node:test';

// Exact full-byte output from pinned Go1.27.3 producer at TypeScript
// 8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f.
// node tools/generate-json-options-map.ts PIN GOLDEN DECLARATIONS
// A nested element-policy mismatch must fail just as a top-level mismatch does.
const goGeneratedSha256 = '0d67a643ca7d3dce6315754e177f7d93cbada15682b4eebc45a2d4eea9a9552c';

test('the whole production option declaration resource matches pinned Go', () => {
  const lock = JSON.parse(readFileSync('upstream.lock.json','utf8'));
  assert.equal(lock.commit,'8d9b6da99ea7d4a8e7d906dd2532456dfe14e24f');
  const raw = readFileSync('src/Tscs.Compiler/json-option-declarations.json');
  assert.equal(createHash('sha256').update(raw).digest('hex'),goGeneratedSha256,
    'Regenerate the full resource with the pinned Go producer; never suppress this integrity check.');
  const declarations = JSON.parse(raw.toString('utf8'));
  assert.equal(declarations.length,126);
  for (const name of ['rootDirs','typeRoots']) {
    const matching = declarations.filter(x => x.name===name);
    assert.equal(matching.length,1);
    assert.equal(matching[0].supported,true);
    assert.equal(matching[0].element?.supported,true);
  }
});
