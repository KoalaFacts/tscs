import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Actual four-module checked ES2015 output executed by Node24; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-numeric-mutation-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'data.ts'),'export const values:number[] = [21,22,-1];\n');
  writeFileSync(join(folder,'compute.ts'),[
    'export function compute(values:number[]):number {',
    '  let total:number=0;',
    '  let i:number=0;',
    '  do {',
    '    if(values[i]<0){i++;continue;}',
    '    total+=values[i];',
    '    i++;',
    '  } while(i<values.length);',
    '  total-=1;',
    '  total*=1;',
    '  return total;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { compute as calculate } from "./compute.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { values } from "./data.js";',
    'import { calculate } from "./bridge.js";',
    'export const result:number=calculate(values);',
    'export const count:number=values.length;'
  ].join('\n'));
  writeFileSync(join(folder,'project.json'),JSON.stringify({
    files:['entry.ts','minimal.d.ts'],
    compilerOptions:{module:'es2015',target:'es2015',noLib:true}
  }));
  const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
  const environment={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
  const compiled=spawnSync(process.env.DOTNET??'dotnet',[compiler,'--emitJs','-p',join(folder,'project.json')],{
    cwd:folder,encoding:'utf8',timeout:30000,env:environment
  });
  assert.equal(compiled.error,undefined,String(compiled.error));
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
  for(const name of ['data','compute','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import {result,count} from "./entry.js"; if(result!==42||count!==3) throw Error("numeric mutation runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);
  writeFileSync(join(folder,'invalid.ts'),'const locked:number=1; locked+=1;');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2588/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  console.log('Numeric mutation four-module ES2015 runtime result=42 count=3; constant mutation rejected without emit.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
