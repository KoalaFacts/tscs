import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {mkdtempSync,mkdirSync,readFileSync,writeFileSync,rmSync,existsSync,unlinkSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {materializePinnedArchive,pinnedArchive} from './materialize-upstream-placeholders.ts';
const sha256=(bytes:Buffer)=>createHash('sha256').update(bytes).digest('hex');
const fixture=(fn:(work:string,parts:string,expected:{sha256:string;bytes:number;parts:number},blob:Buffer)=>void)=>{
 const work=mkdtempSync(join(tmpdir(),'tscs-upstream-zip-'));
 try{
  const parts=join(work,'parts');mkdirSync(parts);
  const blob=Buffer.from('exact upstream test fixture');
  writeFileSync(join(parts,'part-00.bin'),blob.subarray(0,12));
  writeFileSync(join(parts,'part-01.bin'),blob.subarray(12));
  fn(work,parts,{sha256:sha256(blob),bytes:blob.length,parts:2},blob);
 }finally{rmSync(work,{recursive:true,force:true});}
};

test('reassembles all ordered binary fragments without losing source bytes',()=>fixture((work,parts,expected,blob)=>{
 const out=join(work,'archive.zip');
 assert.deepEqual(materializePinnedArchive(parts,out,expected),{
  archiveSha256:expected.sha256,archiveBytes:blob.length,archiveFragments:2});
 assert.deepEqual(readFileSync(out),blob);
}));
test('rejects omitted fragment, preserving no output',()=>fixture((work,parts,expected)=>{
 unlinkSync(join(parts,'part-01.bin'));
 const out=join(work,'archive.zip');
 assert.throws(()=>materializePinnedArchive(parts,out,expected),/Missing|extra/);
 assert.equal(existsSync(out),false);
}));
test('rejects renamed/extra fragments and altered bytes without output',()=>fixture((work,parts,expected)=>{
 const out=join(work,'archive.zip');
 writeFileSync(join(parts,'part-02.bin'),Buffer.from('x'));
 assert.throws(()=>materializePinnedArchive(parts,out,expected),/Missing|extra/);
 unlinkSync(join(parts,'part-02.bin'));
 writeFileSync(join(parts,'part-01.bin'),Buffer.alloc(expected.bytes-12,0));
 assert.throws(()=>materializePinnedArchive(parts,out,expected),/SHA-256 mismatch/);
 assert.equal(existsSync(out),false);
}));
test('rejects truncated and oversized archives',()=>fixture((work,parts,expected)=>{
 const out=join(work,'archive.zip');
 writeFileSync(join(parts,'part-01.bin'),Buffer.from('x'));
 assert.throws(()=>materializePinnedArchive(parts,out,expected),/byte count mismatch/);
 writeFileSync(join(parts,'part-01.bin'),Buffer.alloc(expected.bytes,0));
 assert.throws(()=>materializePinnedArchive(parts,out,expected),/oversized/);
}));
test('does not overwrite or silently accept a replaced output',()=>fixture((work,parts,expected)=>{
 const out=join(work,'archive.zip');writeFileSync(out,'keep');
 assert.throws(()=>materializePinnedArchive(parts,out,expected),/overwrite/);
 assert.equal(readFileSync(out,'utf8'),'keep');
}));
test('checked-in pinned contract preserves the historical source-bound archive hash',()=>{
 assert.deepEqual(pinnedArchive,{sha256:'e45e033d458a318b792777f7d141973e5d457f4f9e1f04801cb4bcf3a6e4512b',bytes:3544490,parts:20});
});
