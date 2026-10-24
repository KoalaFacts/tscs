import test from 'node:test';
import assert from 'node:assert/strict';
import {readdirSync,readFileSync} from 'node:fs';

const root=new URL('../',import.meta.url);
test('owned tool modules and tests use Node native TypeScript entries',()=>{
  assert.ok(Number(process.versions.node.split('.')[0])>=24);
  const files=readdirSync(new URL('tools/',root));
  assert.deepEqual(files.filter(name=>name.endsWith('.mjs')),[]);
  for(const name of ['inventory-ts7.ts','ts7-configurations.ts','ts7-scope.ts','verify-conformance.ts','conformance.ts'])assert.ok(files.includes(name),name);
});
test('shell verification and reference build run native TypeScript without a loader',()=>{
  for(const path of ['scripts/verify.sh','scripts/build-oracle.sh']){
    const text=readFileSync(new URL(path,root),'utf8');
    assert.doesNotMatch(text,/tools\/[^\s]*\.mjs/);
    assert.doesNotMatch(text,/--loader|tsx |ts-node/);
  }
  assert.match(readFileSync(new URL('scripts/verify.sh',root),'utf8'),/node --test tools\/\*\.test\.ts/);
});
