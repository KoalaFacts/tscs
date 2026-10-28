import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

// Real checked TS multi-module graph -> ES2015 -> Node; not original TS7 acceptance.
const folder=mkdtempSync(join(tmpdir(),'tscs-classic-for-'));
try {
  writeFileSync(join(folder,'package.json'),'{"type":"module"}\n');
  writeFileSync(join(folder,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
  writeFileSync(join(folder,'data.ts'),'export const values:number[] = [21,-1,21];\n');
  writeFileSync(join(folder,'sum.ts'),[
    'export function sum(values:number[]):number {',
    '  let total:number=0;',
    '  for(let i:number=0;i<values.length;i++){',
    '    if(values[i]<0){continue;}',
    '    total=total+values[i];',
    '    if(total>40){break;}',
    '  }',
    '  return total;',
    '}'
  ].join('\n'));
  writeFileSync(join(folder,'bridge.ts'),'export { sum as accumulate } from "./sum.js";\n');
  writeFileSync(join(folder,'entry.ts'),[
    'import { values } from "./data.js";',
    'import { accumulate } from "./bridge.js";',
    'export const result:number=accumulate(values);',
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
  for(const name of ['data','sum','bridge','entry'])
    assert.ok(existsSync(join(folder,name+'.js')),'missing '+name+'.js');
  const run=spawnSync(process.execPath,['--input-type=module','--eval',
    'import { result,count } from "./entry.js"; if(result!==42||count!==3) throw Error("classic for runtime mismatch");'],
    {cwd:folder,encoding:'utf8',timeout:15000});
  assert.equal(run.error,undefined,String(run.error));
  assert.equal(run.status,0,run.stdout+run.stderr);

  writeFileSync(join(folder,'invalid.ts'),
    'function bad(xs:number[]):number { for(let i=0;i<xs.length;i++){ xs[i]="bad"; } return 0; }');
  const rejected=spawnSync(process.env.DOTNET??'dotnet',
    [compiler,'--emitJs','--noLib','--target','es2015',join(folder,'invalid.ts'),join(folder,'minimal.d.ts')],
    {cwd:folder,encoding:'utf8',timeout:30000,env:environment});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(folder,'invalid.js')),false);
  console.log('Classic for ES2015 modules executed result=42 count=3; incorrect indexed assignment rejected without emit.');
} finally {
  rmSync(folder,{recursive:true,force:true});
}
