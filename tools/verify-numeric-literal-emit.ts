import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module ES2015 runtime; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-literals-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Value = { base: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Value } from "./model.js";',
    'export function scale(v: Value, factor: number): number { return v.base * factor; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { scale } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { scale } from "./bridge.js";',
    'const data = { base: 84 };',
    'export const result: number = scale(data, .5);',
    'export const decimal: number = .125 + .375;',
    'export const scientific: number = 1.5e2 - 108;',
    'export const tiny: number = 2.5E-1;',
    'export const trailing: number = 3.;',
    'export const leading: number = .5;',
    'export const exact: 100 = 1e2;',
    'export const negative: -1.5 = -1.50;',
    'let steps: number = 0;',
    'for (let i: number = .5; i < 3; i += .5) { steps += 1; }',
    'export const count: number = steps;',
    'export const separated: number = 1_000 + 2_0.5_0;',
    'export const exponentSeparated: number = 1_2.5_0e1_0;',
    'export const keyed: { 10: number } = { 1_0: 42 };',
    'export const indexed: number = keyed[1_0];',
    'export const hex: number = 0X2_A;',
    'export const binary: number = 0b10_1010;',
    'export const octal: number = 0O5_2;',
    'export const radixExact: 42 = 0x2a;',
    'export const radixKeyed: { 10: number } = { 0xA: 21 };',
    'export const radixIndexed: number = radixKeyed[0b1010];',
    'export const radixCrossModule: number = scale({ base: 0o52 }, 0b1);'
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
  assert.doesNotMatch(readFileSync(join(folder,'entry.js'),'utf8'), /\d_\d/,
    'ES2015 output must not contain numeric separator syntax');
  assert.doesNotMatch(readFileSync(join(folder,'entry.js'),'utf8'), /\b0[xXbBoO][0-9a-fA-F_]+/,
    'radix literals must emit checked decimal Number values');
  const execute=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,decimal,scientific,tiny,trailing,leading,exact,negative,count,separated,exponentSeparated,indexed,hex,binary,octal,radixExact,radixIndexed,radixCrossModule} from "./entry.js"; if(result!==42||decimal!==.5||scientific!==42||tiny!==.25||trailing!==3||leading!==.5||exact!==100||negative!==-1.5||count!==5||separated!==1020.5||exponentSeparated!==125000000000||indexed!==42||hex!==42||binary!==42||octal!==42||radixExact!==42||radixIndexed!==21||radixCrossModule!==42) throw Error("numeric literal runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(execute.error,undefined,String(execute.error));
  assert.equal(execute.status,0,execute.stdout+execute.stderr);
  for(const [name,source,exit,pattern] of [
    ['invalid-type','let n: string = 0.5;',2,/TS2322/],
    ['invalid-literal','type Half = .5; let n: Half = 0.25;',2,/TS2322/],
    ['invalid-exponent','let n: number = 1e+;',1,/TSCS1001/],
    ['invalid-separator','let n: number = 1__000;',1,/TSCS1001/],
    ['invalid-exponent-separator','let n: number = 1e_2;',1,/TSCS1001/],
    ['separator-type','let n: string = 1_000;',2,/TS2322/],
    ['radix-type','let n: string = 0x2a;',2,/TS2322/],
    ['radix-literal-type','type Answer = 0b101010; let n: Answer = 0o53;',2,/TS2322/],
    ['invalid-radix','let n: number = 0x_FF;',1,/TSCS1001/],
    ['invalid-base-digit','let n: number = 0b102;',1,/TSCS1001/],
    ['unsafe-radix','let n: number = 0x20000000000000;',1,/TSCS1001/]
  ] as const) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015',
        join(folder,name+'.ts'),join(folder,'minimal.d.ts')],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(rejected.error,undefined,String(rejected.error));
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,pattern);
    assert.equal(existsSync(join(folder,name+'.js')),false);
  }
  console.log('numeric literals: four-module ES2015 result=42 decimal=0.5 scientific=42 tiny=0.25 trailing=3 count=5; four negative no-emit');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
