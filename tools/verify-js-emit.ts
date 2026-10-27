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
  // The object graph must pass through the actual C# compiler and execute
  // as ES2015 modules; acceptance against the frozen TS7 suite is separate.
  writeFileSync(join(temporary,"object-data.ts"),
    'export const order: { quantity: number; owner: string } = { quantity: 21, owner: "Ada" };\n');
  writeFileSync(join(temporary,"object-service.ts"),[
    'import { order } from "./object-data.js";',
    'export function double(input: { quantity: number }): { quantity: number } {',
    '  return { quantity: input.quantity * 2 };',
    '}',
    'export const processed: { quantity: number } = double(order);'
  ].join("\n")+"\n");
  writeFileSync(join(temporary,"object-entry.ts"),[
    'import { processed } from "./object-service.js";',
    'import { order } from "./object-data.js";',
    'let state: { quantity: number } = { quantity: 0 };',
    'state.quantity = processed.quantity;',
    'export const answer: number = state.quantity;',
    'export const owner: string = order.owner;'
  ].join("\n")+"\n");
  const objectCompile=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--emitJs","--module","es2015","--noLib","--target","es2015",
    join(temporary,"object-entry.ts"),globals
  ],{encoding:"utf8",timeout:30000,env:{
    ...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:"1",TESTINGPLATFORM_TELEMETRY_OPTOUT:"1"
  }});
  assert.equal(objectCompile.error,undefined,String(objectCompile.error));
  assert.equal(objectCompile.status,0,objectCompile.stdout+"\n"+objectCompile.stderr);
  for(const file of ["object-data.js","object-service.js","object-entry.js"])
    assert.ok(existsSync(join(temporary,file)),file+" was not emitted");
  const objectRun=spawnSync(process.execPath,[
    "--input-type=module","--eval",
    'import { answer, owner } from "./object-entry.js"; if(answer !== 42 || owner !== "Ada") throw new Error("object module result mismatch");'
  ],{cwd:temporary,encoding:"utf8",timeout:15000});
  assert.equal(objectRun.error,undefined,String(objectRun.error));
  assert.equal(objectRun.status,0,objectRun.stdout+"\n"+objectRun.stderr);

  const invalidObject=join(temporary,"invalid-object.ts");
  writeFileSync(invalidObject,
    'const item: { quantity: number } = { quantity: 1 }; let bad: number = item.missing;\n');
  const negative=spawnSync(process.env.DOTNET??"dotnet",[
    resolve("src/Tscs.Cli/bin/Release/net10.0/tscs.dll"),
    "--noEmit","--noLib","--target","es2015",invalidObject,globals
  ],{encoding:"utf8",timeout:30000,env:{
    ...process.env,DOTNET_CLI_TELEMETRY_OPTOUT:"1",TESTINGPLATFORM_TELEMETRY_OPTOUT:"1"
  }});
  assert.equal(negative.error,undefined,String(negative.error));
  assert.notEqual(negative.status,0,"missing object member unexpectedly compiled");
  assert.match(negative.stdout+"\n"+negative.stderr,/TS2339/);
  console.log("AST-backed ES2015 script and named module graphs emitted and executed successfully.");
} finally {
  rmSync(temporary,{recursive:true,force:true});
}
