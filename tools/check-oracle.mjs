// Read-only comparison: this command never updates frozen expected results.
import {readFileSync,writeFileSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {isDeepStrictEqual} from 'node:util';
import {execute,validateSelection} from './conformance.mjs';
const read=p=>JSON.parse(readFileSync(p));
const manifest=read('tests/conformance/manifest.json'),expected=read('tests/conformance/expected.json');
validateSelection(manifest.cases);
for(const c of manifest.cases){
 const actual=execute('.artifacts/tscs-oracle',[c.file,'fixtures/minimal.d.ts']);
 if(!isDeepStrictEqual(actual,expected[c.id]?.result))throw Error('Reference diagnostics changed: '+c.id);
 const cli=spawnSync('.artifacts/tsgo',['--noEmit','--noLib','--target','es2015','--pretty','false',c.file,'fixtures/minimal.d.ts'],{encoding:'utf8',timeout:10000});
 if(cli.error||cli.signal||cli.stdout!==expected[c.id].stdout||cli.stderr!==expected[c.id].stderr||cli.status!==actual.exitCode)throw Error('Reference CLI changed: '+c.id);
}
const hash=p=>createHash('sha256').update(readFileSync(p)).digest('hex');
writeFileSync('.artifacts/reference-build.json',JSON.stringify({upstreamCommit:manifest.upstreamCommit,oracleSha256:hash('.artifacts/tscs-oracle'),tsgoSha256:hash('.artifacts/tsgo'),cases:manifest.cases.length},null,2)+'\n');
console.log(`Pinned reference reproduced ${manifest.cases.length} frozen cases.`);
