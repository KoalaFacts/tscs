import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Real four-module ES2015 execution; invalid mutations never emit JavaScript.
const folder=mkdtempSync(join(tmpdir(),'tscs-string-primitives-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/string-primitives/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Item = { text: string };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Item } from "./model.js";',
    'export function countLowercase(item: Item): number {',
    '  let score: number = 0;',
    '  for (let i: number = 0; i < item.text.length; i++) {',
    '    if (item.text[i] >= "a" && item.text[i] <= "z") { score += 1; }',
    '  }',
    '  return score;',
    '}',
    'export function earlier(a: string, b: string): boolean { return a < b; }',
    'export function middle(item: Item): string { return item.text[1]; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { countLowercase, earlier, middle } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { countLowercase, earlier, middle } from "./bridge.js";',
    'const item = { text: "bAz" };',
    'export const result: number = countLowercase(item);',
    'export const ordered: boolean = earlier("alpha", "beta");',
    'export const ascii: boolean = earlier("Z", "a");',
    'export const selected: string = middle(item);',
    'export const length: number = item.text.length;',
    'export const fromLiteral: string = "abc"[1];',
    'export const same: boolean = "a" <= "a" && "z" >= "y";'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compiled=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','-p',join(folder,'project.json')],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(compiled.error,undefined,String(compiled.error));
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const executed=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,ordered,ascii,selected,length,fromLiteral,same} from "./entry.js"; if(result!==2||ordered!==true||ascii!==true||selected!=="A"||length!==3||fromLiteral!=="b"||same!==true) throw Error("string primitive runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(executed.error,undefined,String(executed.error));
  assert.equal(executed.status,0,executed.stdout+executed.stderr);
  const invalid=[
    ['read-only-length','let s: string = "abc"; s.length = 2;','TS2540'],
    ['compound-length','let s: string = "abc"; s.length += 1;','TS2540'],
    ['read-only-index','let s: string = "abc"; s[0] = "z";','TS2542'],
    ['compound-index','let s: string = "abc"; s[0] += "z";','TS2542'],
    ['mixed-types','let b: boolean = "a" < 2;','TSCS1001'],
    ['wrong-result','let n: number = "a" < "b";','TS2322']
  ];
  for(const [name,source,expected] of invalid) {
    const path=join(folder,name+'.ts');
    writeFileSync(path,source);
    const rejected=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015',path,join(folder,'minimal.d.ts')],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(rejected.error,undefined,String(rejected.error));
    // TSCS1001 reports an unsupported subset operation (CLI exit 1);
    // TS diagnostics report semantic mismatches (exit 2). Both must fail
    // without emitting output and carry their exact non-swallowed code.
    assert.equal(rejected.status,expected.startsWith('TSCS')?1:2,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,new RegExp(expected));
    assert.equal(existsSync(join(folder,name+'.js')),false,name+' unexpectedly emitted JavaScript');
  }
  console.log('string primitives: four-module Node ES2015 result=2, ordered=true, selected=A, length=3; 6 invalid programs blocked JS emit.');
} finally {rmSync(folder,{recursive:true,force:true});}
