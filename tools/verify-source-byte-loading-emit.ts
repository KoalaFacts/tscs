import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Real compiler -> four-byte/source decoder -> ES2015 module JS -> Node.
const folder=mkdtempSync(join(tmpdir(),'tscs-source-bytes-'));
const dll=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
function encoded(text:string,kind:'utf8-bom'|'utf16-le'|'utf16-be'):Buffer {
  if(kind==='utf8-bom')return Buffer.concat([Buffer.from([0xef,0xbb,0xbf]),Buffer.from(text,'utf8')]);
  const bytes=Buffer.from(text,'utf16le');
  if(kind==='utf16-be')bytes.swap16();
  return Buffer.concat([Buffer.from(kind==='utf16-be'?[0xfe,0xff]:[0xff,0xfe]),bytes]);
}
function compile(source:string) {
  const result=spawnSync(process.env.DOTNET??'dotnet',
    [dll,'--emitJs','--noLib','--target','es2015','--module','es2015',source,'minimal.d.ts'],
    {cwd:folder,encoding:'utf8',timeout:30000,maxBuffer:4*1024*1024,env});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.signal,null,'compiler crashed or timed out');
  return result;
}
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),encoded(readFileSync('fixtures/minimal.d.ts','utf8'),'utf8-bom'));
  writeFileSync(join(folder,'logic.ts'),encoded('// 🌏 UTF-16 LE with BOM\nexport function double(n:number):number {return n*2;}\n','utf16-le'));
  writeFileSync(join(folder,'bridge.ts'),encoded('export {double} from "./logic.js";\n','utf8-bom'));
  writeFileSync(join(folder,'entry.ts'),encoded('// 😀 UTF-16 BE with BOM\nimport {double} from "./bridge.js";\nexport const result:number=double(21);\n','utf16-be'));
  const built=compile('entry.ts');
  assert.equal(built.status,0,built.stdout+built.stderr);
  for(const name of ['logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing emitted '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result} from "./entry.js"; if(result!==42)throw Error("wrong decoded output: "+result);'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);

  writeFileSync(join(folder,'invalid.ts'),encoded('let value:number="wrong";\n','utf16-be'));
  const invalid=compile('invalid.ts');
  assert.equal(invalid.status,2,invalid.stdout+invalid.stderr);
  assert.match(invalid.stdout+invalid.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false,'invalid source must not emit');

  writeFileSync(join(folder,'malformed.ts'),Buffer.from([0x6c,0x65,0x74,0x20,0x61,0x3d,0xc3,0x28,0x3b]));
  const malformed=compile('malformed.ts');
  assert.equal(malformed.status,1,malformed.stdout+malformed.stderr);
  assert.match(malformed.stdout+malformed.stderr,/TSCS1002/);
  assert.equal(existsSync(join(folder,'malformed.js')),false,'invalid bytes must not emit');
  console.log('Source decoding: three real ES2015 modules (UTF-16 LE/BE, UTF-8 BOM) execute result=42; semantic and malformed-byte negatives block JS.');
} finally {rmSync(folder,{recursive:true,force:true});}
