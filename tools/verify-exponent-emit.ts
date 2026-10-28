import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Four-module ES2015 runtime and negative no-emit; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-exponent-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'model.ts'),'export type Payload = { value: number };\n');
  writeFileSync(join(folder,'logic.ts'),[
    'import type { Payload } from "./model.js";',
    'export function square(data: Payload, __tscs_power_0: number): Payload { data.value **= __tscs_power_0; return data; }',
    'export function power(a: number, b: number): number { return a ** b; }'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { square, power } from "./logic.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { square, power } from "./bridge.js";',
    'const data = { value: 6 };',
    'export const result: number = square(data, 2).value + power(2, 1) * 3;',
    'export const right: number = 2 ** 3 ** 2;',
    'export const left: number = (2 ** 3) ** 2;',
    'export const inverse: number = 2 ** -2;',
    'export const grouped: number = (-2) ** 2;',
    'export const negative: number = -(2 ** 2);',
    'export const priority: number = 3 * 2 ** 3 + 1;',
    'let values: number[] = [2, 3];',
    'values[0] **= values[1];',
    'export const indexed: number = values[0];',
    'let steps: number = 0;',
    'for (let i: number = 2; i < 16; i **= 2) { steps += 1; }',
    'export const count: number = steps;',
    'let receiverCalls: number = 0;',
    'let indexCalls: number = 0;',
    'let subject = { value: 3 };',
    'function receiver(): { value: number } { receiverCalls += 1; return subject; }',
    'receiver().value **= 2;',
    'let another: number[] = [3];',
    'function selectIndex(): number { indexCalls += 1; return 0; }',
    'another[selectIndex()] **= 2;',
    'export const receiverOnce: number = receiverCalls;',
    'export const indexOnce: number = indexCalls;',
    'export const mutated: number = subject.value + another[0];'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compiled=spawnSync(process.env.DOTNET??'dotnet',[compiler,'--emitJs','-p',join(folder,'project.json')],
    {cwd:folder,encoding:'utf8',timeout:30000,env});
  assert.equal(compiled.error,undefined,String(compiled.error));
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['model','logic','bridge','entry']) {
    const generated=join(folder,name+'.js');
    assert.ok(existsSync(generated),'missing '+name+'.js');
    assert.doesNotMatch(readFileSync(generated,'utf8'),/\*\*/,name+' contains ES2016 exponent syntax');
  }
  assert.match(readFileSync(join(folder,'logic.js'),'utf8'),/Math\.pow\(/);
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,right,left,inverse,grouped,negative,priority,indexed,count,receiverOnce,indexOnce,mutated} from "./entry.js"; if(result!==42||right!==512||left!==64||inverse!==0.25||grouped!==4||negative!==-4||priority!==25||indexed!==8||count!==2||receiverOnce!==1||indexOnce!==1||mutated!==18) throw Error("numeric exponent runtime mismatch");'],{cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  for(const [name,source,exit,pattern] of [
    ['invalid-type','let n: string = 2 ** 3;',2,/TS2322/],
    ['invalid-const','const n: number = 2; n **= 3;',2,/TS2588/],
    ['invalid-unary','let n: number = -2 ** 2;',1,/TSCS1001/]
  ] as const) {
    writeFileSync(join(folder,name+'.ts'),source);
    const rejected=spawnSync(process.env.DOTNET??'dotnet',
      [compiler,'--emitJs','--noLib','--target','es2015',join(folder,name+'.ts'),join(folder,'minimal.d.ts')],
      {cwd:folder,encoding:'utf8',timeout:30000,env});
    assert.equal(rejected.error,undefined,String(rejected.error));
    assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
    assert.match(rejected.stdout+rejected.stderr,pattern);
    assert.equal(existsSync(join(folder,name+'.js')),false);
  }
  console.log('numeric exponentiation: four-module ES2015 result=42 right=512 left=64 inverse=0.25 grouped=4 negative=-4 priority=25 indexed=8 count=2; three negative no-emit checks');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
