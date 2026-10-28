import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

// Genuine end-to-end smoke: the C# compiler emits checked, nonempty script
// JavaScript, which Node executes. Independent TS7 acceptance is separate.
const temporary=mkdtempSync(join(tmpdir(),"tscs-js-emit-"));
try {
  const source=join(temporary,"main.ts");
  const globals=join(temporary,"minimal.d.ts");
  const javascript=join(temporary,"main.js");
  writeFileSync(source,[
    "function twice(x: number): number { return x + x; }",
    "function choose(flag: boolean): number {",
    "  if (flag && 2 > 1) { return twice(21); } else { return 0; }",
    "}",
    "let answer: number = choose(true);"
  ].join("\n")+"\n");
  writeFileSync(globals,"interface Array<T> {} interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
  const compiler=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--emitJs","--noLib","--target","es2015",source,globals
  ],{encoding:"utf8",timeout:30000,env:{
    ...process.env,
    DOTNET_CLI_TELEMETRY_OPTOUT:"1",
    TESTINGPLATFORM_TELEMETRY_OPTOUT:"1"
  }});
  assert.equal(compiler.error,undefined,String(compiler.error));
  assert.equal(compiler.status,0,compiler.stdout+"\n"+compiler.stderr);
  assert.ok(existsSync(javascript),"AST-backed .js was not written");

  // Run the output instead of merely checking that an expected string exists.
  const result=spawnSync(process.execPath,[
    "--input-type=commonjs","--eval",
    readFileSync(javascript,"utf8")+"\nif (answer !== 42) throw new Error('incorrect emitted result');"
  ],{encoding:"utf8",timeout:15000});
  assert.equal(result.error,undefined,String(result.error));
  assert.equal(result.status,0,result.stdout+"\n"+result.stderr);
  // An empty statement before a string expression prevents it from becoming
  // a strict directive. Running the result catches incorrect AST erasure.
  const nonDirective=join(temporary,"non-directive.ts");
  writeFileSync(nonDirective,'; "use strict"; function identity(eval: number): number { return eval; } let answer: number = identity(42);\n');
  const nonDirectiveCompile=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--emitJs","--noLib","--target","es2015",nonDirective,globals
  ],{encoding:"utf8",timeout:30000,env:{
    ...process.env,
    DOTNET_CLI_TELEMETRY_OPTOUT:"1",
    TESTINGPLATFORM_TELEMETRY_OPTOUT:"1"
  }});
  assert.equal(nonDirectiveCompile.error,undefined,String(nonDirectiveCompile.error));
  assert.equal(nonDirectiveCompile.status,0,nonDirectiveCompile.stdout+"\n"+nonDirectiveCompile.stderr);
  const second=spawnSync(process.execPath,[
    "--input-type=commonjs","--eval",
    readFileSync(join(temporary,"non-directive.js"),"utf8")+
      "\nif (answer !== 42) throw new Error('incorrect emitted result');"
  ],{encoding:"utf8",timeout:15000});
  assert.equal(second.error,undefined,String(second.error));
  assert.equal(second.status,0,second.stdout+"\n"+second.stderr);
  // Cross-file compilation uses the same module-linked program and writes
  // real ES2015 modules. .js specifiers resolve to .ts inputs during checking
  // but remain .js at runtime; Node executes the emitted graph unmodified.
  writeFileSync(join(temporary,"package.json"),'{"type":"module"}\n');
  writeFileSync(join(temporary,"value.ts"),"export const value: number = 21;\n");
  writeFileSync(join(temporary,"bridge.ts"),'export { value as answer } from "./value.js";\n');
  const moduleEntry=join(temporary,"entry.ts");
  writeFileSync(moduleEntry,'import { answer } from "./bridge.js"; export function twice(x: number): number { return x + x; } export const result: number = twice(answer);\n');
  // The config project and CLI share the same effective ES2015 module mode.
  // This exercises config input, transitive source loading and real JS output
  // in one compiled-and-executed path (not just an option parsing snapshot).
  const project=join(temporary,"tsconfig.json");
  writeFileSync(project,JSON.stringify({
    files:["entry.ts","minimal.d.ts"],
    compilerOptions:{module:"es2015",target:"es2015",noLib:true}
  }));
  const moduleCompile=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--emitJs","-p",project
  ],{encoding:"utf8",timeout:30000,env:{
    ...process.env,
    DOTNET_CLI_TELEMETRY_OPTOUT:"1",
    TESTINGPLATFORM_TELEMETRY_OPTOUT:"1"
  }});
  assert.equal(moduleCompile.error,undefined,String(moduleCompile.error));
  assert.equal(moduleCompile.status,0,moduleCompile.stdout+"\n"+moduleCompile.stderr);
  for(const file of ["value.js","bridge.js","entry.js"])
    assert.ok(existsSync(join(temporary,file)),file+" was not emitted");
  const moduleRun=spawnSync(process.execPath,[
    "--input-type=module","--eval",
    'import { result } from "./entry.js"; if (result !== 42) throw new Error("wrong ESM runtime result");'
  ],{cwd:temporary,encoding:"utf8",timeout:15000});
  assert.equal(moduleRun.error,undefined,String(moduleRun.error));
  assert.equal(moduleRun.status,0,moduleRun.stdout+"\n"+moduleRun.stderr);
  // Object data follows exactly that same project/compiler/ESM pipeline.
  // Mutation of an imported const object's property must not be confused with
  // rebinding its import. A wider variable may satisfy a narrower shape.
  writeFileSync(join(temporary,"object-data.ts"),
    'export const seed = { value: 20, label: "seed", nested: { enabled: true } };\n');
  writeFileSync(join(temporary,"object-transform.ts"),
    'export function twice(data: { value: number }): { value: number } { return { value: data.value * 2 }; }\n');
  writeFileSync(join(temporary,"object-bridge.ts"),
    'export { twice as transform } from "./object-transform.js";\n');
  writeFileSync(join(temporary,"object-entry.ts"),[
    'import { seed } from "./object-data.js";',
    'import { transform } from "./object-bridge.js";',
    'seed.value = 21;',
    'seed.nested.enabled = false;',
    'export const result: number = transform(seed).value;',
    'export const label: string = seed.label;',
    'export const enabled: boolean = seed.nested.enabled;'
  ].join("\n"));
  const objectProject=join(temporary,"objects.json");
  writeFileSync(objectProject,JSON.stringify({
    files:["object-entry.ts","minimal.d.ts"],
    compilerOptions:{module:"es2015",target:"es2015",noLib:true}
  }));
  const objectCompile=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),"--emitJs","-p",objectProject
  ],{encoding:"utf8",timeout:30000});
  assert.equal(objectCompile.error,undefined,String(objectCompile.error));
  assert.equal(objectCompile.status,0,objectCompile.stdout+"\n"+objectCompile.stderr);
  const objectRun=spawnSync(process.execPath,["--input-type=module","--eval",
    'import { result,label,enabled } from "./object-entry.js"; '+
    'if (result !== 42 || label !== "seed" || enabled !== false) throw Error("object data runtime mismatch");'
  ],{cwd:temporary,encoding:"utf8",timeout:15000});
  assert.equal(objectRun.error,undefined,String(objectRun.error));
  assert.equal(objectRun.status,0,objectRun.stdout+"\n"+objectRun.stderr);
  // Errors must prevent all output, rather than merely returning exit 2 after
  // partially writing an executable program.
  const invalid=join(temporary,"invalid-object.ts");
  writeFileSync(invalid,'const data = { value: 1 }; data.value = "wrong";');
  const rejected=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--emitJs","--noLib","--target","es2015",invalid,globals
  ],{encoding:"utf8",timeout:30000});
  assert.equal(rejected.error,undefined,String(rejected.error));
  assert.equal(rejected.status,2,rejected.stdout+"\n"+rejected.stderr);
  assert.match(rejected.stdout+rejected.stderr,/TS2322/);
  assert.equal(existsSync(join(temporary,"invalid-object.js")),false);
  // Indexed arrays and for..of share the same checked module-graph output.
  writeFileSync(join(temporary,"array-data.ts"),
    'export const values = [20, 21];\n');
  writeFileSync(join(temporary,"array-sum.ts"),[
    'export function sum(values: number[]): number {',
    '  let total: number = 0;',
    '  for (const current of values) { total = total + current; }',
    '  return total;',
    '}'
  ].join("\n"));
  writeFileSync(join(temporary,"array-bridge.ts"),
    'export { sum as accumulate } from "./array-sum.js";\n');
  writeFileSync(join(temporary,"array-entry.ts"),[
    'import { values } from "./array-data.js";',
    'import { accumulate } from "./array-bridge.js";',
    'values[0] = 21;',
    'export const result: number = accumulate(values);',
    'export const first: number = values[0];',
    'export const length: number = values.length;'
  ].join("\n"));
  const arrayProject=join(temporary,"arrays.json");
  writeFileSync(arrayProject,JSON.stringify({
    files:["array-entry.ts","minimal.d.ts"],
    compilerOptions:{module:"es2015",target:"es2015",noLib:true}
  }));
  const arrayCompile=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),"--emitJs","-p",arrayProject
  ],{encoding:"utf8",timeout:30000});
  assert.equal(arrayCompile.error,undefined,String(arrayCompile.error));
  assert.equal(arrayCompile.status,0,arrayCompile.stdout+"\n"+arrayCompile.stderr);
  for(const name of ["array-data","array-sum","array-bridge","array-entry"])
    assert.ok(existsSync(join(temporary,name+".js")),name+" was not emitted");
  const arrayRun=spawnSync(process.execPath,["--input-type=module","--eval",
    'import { result, first, length } from "./array-entry.js"; ' +
    'if (result !== 42 || first !== 21 || length !== 2) throw Error("array iteration runtime mismatch");'
  ],{cwd:temporary,encoding:"utf8",timeout:15000});
  assert.equal(arrayRun.error,undefined,String(arrayRun.error));
  assert.equal(arrayRun.status,0,arrayRun.stdout+"\n"+arrayRun.stderr);
  const invalidArray=join(temporary,"invalid-array.ts");
  writeFileSync(invalidArray,'const xs = [1,2]; xs[0] = "bad";');
  const rejectedArray=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--emitJs","--noLib","--target","es2015",invalidArray,globals
  ],{encoding:"utf8",timeout:30000});
  assert.equal(rejectedArray.error,undefined,String(rejectedArray.error));
  assert.equal(rejectedArray.status,2,rejectedArray.stdout+"\n"+rejectedArray.stderr);
  assert.match(rejectedArray.stdout+rejectedArray.stderr,/TS2322/);
  assert.equal(existsSync(join(temporary,"invalid-array.js")),false);
  console.log("AST-backed ES2015 scripts, objects and array for..of emitted and executed (array sum=42, first=21, length=2).");
} finally {
  rmSync(temporary,{recursive:true,force:true});
}
