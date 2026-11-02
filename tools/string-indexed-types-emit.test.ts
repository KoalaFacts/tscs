import assert from 'node:assert/strict';
import {test} from 'node:test';
import {spawnSync} from 'node:child_process';
import {existsSync,mkdtempSync,readFileSync,rmSync,writeFileSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';

test('string type-index selectors check and execute through linked ES2015 modules',()=>{
  const dir=mkdtempSync(join(tmpdir(),'tscs-string-index-type-'));
  try {
    writeFileSync(join(dir,'package.json'),'{"type":"module"}\n');
    writeFileSync(join(dir,'minimal.d.ts'),readFileSync('fixtures/string-indexed-types/minimal.d.ts'));
    writeFileSync(join(dir,'model.ts'),[
      'export type Text=string;',
      'export type Character=Text[number];',
      'export type Length=Text["length"];',
      'export type Mixed=Text[number|"length"];',
      'export type LiteralChar="hello"[0];',
      'export const phrase:Text="hello";'
    ].join('\n'));
    writeFileSync(join(dir,'logic.ts'),[
      'import type {Text,Character,Length,Mixed} from "./model.js";',
      'export function pick(text:Text):Character{return text[1];}',
      'export function measure(text:Text):Length{return text.length;}',
      'export function carry(value:Mixed):Mixed{return value;}'
    ].join('\n'));
    writeFileSync(join(dir,'bridge.ts'),'export {pick,measure,carry} from "./logic.js";\n');
    writeFileSync(join(dir,'entry.ts'),[
      'import {phrase} from "./model.js";',
      'import type {Character,Length,Mixed,LiteralChar} from "./model.js";',
      'import {pick,measure,carry} from "./bridge.js";',
      'export const char:Character=pick(phrase);',
      'export const length:Length=measure(phrase);',
      'export const mixed:Mixed=carry("z");',
      'export const literal:LiteralChar=char;'
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
    const result=compile(['--emitJs','-p',join(dir,'project.json')]);
    assert.equal(result.status,0,result.stdout+result.stderr);
    for(const name of ['model','logic','bridge','entry'])
      assert.ok(existsSync(join(dir,name+'.js')),'missing linked module '+name);
    const run=spawnSync(process.execPath,['--input-type=module','--eval',
      'import {char,length,mixed,literal} from "./entry.js"; if(char!=="e"||length!==5||mixed!=="z"||literal!=="e")throw Error("string indexed type runtime mismatch");'],
      {cwd:dir,encoding:'utf8',timeout:15000});
    assert.equal(run.error,undefined,String(run.error));
    assert.equal(run.status,0,run.stdout+run.stderr);
    const invalid:[string,string,string,number][]=[
      ['wrong-code-unit','type C=string[number];let v:C=42;','TS2322',2],
      ['wrong-length','type C=string["length"];let v:C="bad";','TS2322',2],
      ['wrong-union','type C=string[number|"length"];let v:C=false;','TS2322',2],
      ['unknown-key','type C=string["missing"];let v:C="bad";','TSCS1001',1],
      ['unsafe-key','type C=string[boolean];let v:C="bad";','TSCS1001',1]
    ];
    for(const [name,source,diagnostic,exit] of invalid){
      const file=join(dir,'bad-'+name+'.ts');
      writeFileSync(file,source);
      const rejected=compile(['--emitJs','--noLib','--target','es2015',file,join(dir,'minimal.d.ts')]);
      assert.equal(rejected.status,exit,rejected.stdout+rejected.stderr);
      assert.match(rejected.stdout+rejected.stderr,new RegExp(diagnostic));
      assert.equal(existsSync(file.replace(/\.ts$/,'.js')),false,'invalid source emitted JavaScript');
    }
  } finally {rmSync(dir,{recursive:true,force:true});}
});
