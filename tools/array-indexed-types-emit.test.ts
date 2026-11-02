import assert from 'node:assert/strict';
import {test} from 'node:test';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

test('array indexed type selections compile through four ES2015 modules and reject invalid programs',()=>{
  const dir=mkdtempSync(join(tmpdir(),'tscs-array-index-type-'));
  try {
    writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
    writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/array-iteration/minimal.d.ts'));
    writeFileSync(join(dir,'model.ts'),[
      'export type Item={value:number};',
      'export type Items=Item[];',
      'export type Value=Items[number]["value"];',
      'export type Word=(readonly string[])[number];',
      'export type Mixed=(number[]|string[])[number];',
      'export type Count=number[]["length"];',
      'export const numbers:number[]=[21,42];'
    ].join('\n'));
    writeFileSync(join(dir,'logic.ts'),[
      'import type {Value,Word,Mixed} from "./model.js";',
      'export function twice(n:Value):Value{return n+n;}',
      'export function echo(word:Word):Word{return word;}',
      'export function choose(value:Mixed):Mixed{return value;}'
    ].join('\n'));
    writeFileSync(join(dir,'bridge.ts'),'export {twice,echo,choose} from "./logic.js";\n');
    writeFileSync(join(dir,'entry.ts'),[
      'import type {Value,Word,Mixed,Count} from "./model.js";',
      'import {numbers} from "./model.js";',
      'import {twice,echo,choose} from "./bridge.js";',
      'export const result:Value=twice(numbers[0]);',
      'export const word:Word=echo("hello");',
      'export const mixed:Mixed=choose("ready");',
      'export const count:Count=numbers.length;'
    ].join('\n'));
    writeFileSync(join(dir,'project.json'),JSON.stringify({
      files:['entry.ts','minimal.d.ts'],compilerOptions:{module:'es2015',target:'es2015',noLib:true}
    }));
    const compiler=resolve('src/Tscs.Cli/bin/Release/net10.0/tscs.dll');
    const env={...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:'1',TESTINGPLATFORM_TELEMETRY_OPTOUT:'1'};
    const compile=(args:string[])=>{
      const r=spawnSync(process.env.DOTNET??'dotnet',[compiler,...args],
        {cwd:dir,encoding:'utf8',timeout:30000,env});
      assert.equal(r.error,undefined,String(r.error));
      assert.equal(r.signal,null,'compiler crash or timeout');
      return r;
    };
    const compiled=compile(['--emitJs','-p',join(dir,'project.json')]);
    assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
    for(const name of ['model','logic','bridge','entry'])
      assert.ok(existsSync(join(dir,name+'.js')),'missing linked module '+name);
    const executed=spawnSync(process.execPath,['--input-type=module','--eval',
      'import {result,word,mixed,count} from "./entry.js"; if(result!==42||word!=="hello"||mixed!=="ready"||count!==2)throw Error("array type index runtime mismatch");'],
      {cwd:dir,encoding:'utf8',timeout:15000});
    assert.equal(executed.error,undefined,String(executed.error));
    assert.equal(executed.status,0,executed.stdout+executed.stderr);
    const invalid:[string,string,string,number][]=[
      ['wrong-element','type V=number[][number];let v:V="bad";','TS2322',2],
      ['wrong-length','type V=number[]["length"];let v:V="bad";','TS2322',2],
      ['bad-key','type V=number[][boolean];let v:V=42;','TSCS1001',1],
      ['missing','type V=number[]["absent"];let v:V=42;','TSCS1001',1],
      ['mixed','type V=(number[]|{x:number})[number];let v:V=42;','TSCS1001',1]
    ];
    for(const [name,source,code,exit] of invalid){
      const file=join(dir,'bad-'+name+'.ts');writeFileSync(file,source);
      const result=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
      assert.equal(result.status,exit,result.stdout+result.stderr);
      assert.match(result.stdout+result.stderr,new RegExp(code));
      assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'invalid source emitted JS');
    }
  } finally {rmSync(dir,{recursive:true,force:true});}
});
