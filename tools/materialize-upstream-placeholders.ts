// Materialize the exact historical, source-bound TS7 configuration-placeholder
// bundle committed as 20 ordered binary fragments. This does not run or pass
// any TypeScript compiler test.
import {createHash} from 'node:crypto';
import {existsSync,readFileSync,readdirSync,writeFileSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';

export const pinnedArchive = Object.freeze({
  sha256: 'e45e033d458a318b792777f7d141973e5d457f4f9e1f04801cb4bcf3a6e4512b',
  bytes: 3544490,
  parts: 20,
});
const demand=(condition:unknown,message:string):asserts condition=>{if(!condition)throw Error(message);};

export function materializePinnedArchive(partsDirectory:string,outputFile:string,
  expected:{sha256:string;bytes:number;parts:number}=pinnedArchive){
  demand(Number.isSafeInteger(expected.parts)&&expected.parts>0&&
    Number.isSafeInteger(expected.bytes)&&expected.bytes>0&&/^[a-f0-9]{64}$/.test(expected.sha256),
    'Invalid expected archive contract');
  const directory=resolve(partsDirectory), destination=resolve(outputFile);
  demand(!existsSync(destination),'Refusing to overwrite an existing archive');
  const names=Array.from({length:expected.parts},(_,index)=>`part-${String(index).padStart(2,'0')}.bin`);
  const actual=readdirSync(directory).sort();
  demand(actual.length===names.length&&actual.every((name,i)=>name===names[i]),
    'Missing, extra or wrongly named archive fragments');
  const hash=createHash('sha256');
  const fragments:Buffer[]=[];
  let size=0;
  for(const name of names){
    const bytes=readFileSync(join(directory,name));
    demand(bytes.length>0&&size+bytes.length<=expected.bytes,
      'Truncated or oversized archive fragment: '+name);
    hash.update(bytes);fragments.push(bytes);size+=bytes.length;
  }
  demand(size===expected.bytes,'Archive byte count mismatch');
  const digest=hash.digest('hex');
  demand(digest===expected.sha256,'Archive SHA-256 mismatch');
  writeFileSync(destination,Buffer.concat(fragments),{flag:'wx'});
  return {archiveSha256:digest,archiveBytes:size,archiveFragments:names.length};
}

if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href){
  try{
    demand(process.argv.length===4,
      'Usage: node tools/materialize-upstream-placeholders.ts PARTS_DIRECTORY NEW_ARCHIVE.zip');
    console.log(JSON.stringify(materializePinnedArchive(process.argv[2],process.argv[3])));
  }catch(error){console.error((error as Error).message);process.exitCode=1;}
}
