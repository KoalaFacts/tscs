using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class JavaScriptEmitTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-emit-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);

    private string Write(string name,string text)
    {
        var file=Path.Combine(directory,name);
        File.WriteAllText(file,text);
        return file;
    }

    private string Globals()=>Write("minimal.d.ts",
        "interface Array<T> {} interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");

    private (int Code,string Output) Run(params string[] arguments)
    {
        using var output=new StringWriter();
        int code=CommandLine.Run(arguments,output,output);
        return (code,output.ToString());
    }

    [Fact]
    public void AmbientVariablesAreErasedFromEmit()
    {
        var input=Write("ambient-variables.ts","declare const value: number;\ndeclare let other: string;\nlet copy: number = value;");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        Assert.Equal("",result.Output);
        Assert.Equal("let copy = value;",File.ReadAllText(Path.ChangeExtension(input,".js")).Replace("\r\n","\n").Replace("\"use strict\";\n","").Trim());
    }

    // compiler/arrayIterationLibES5TargetDifferent.ts (nolib=true,target=es2015):
    // TS5053 precedes the missing --noLib globals and does not block emit.
    [Fact]
    public void LibWithNoLibReportsTs5053BeforeMissingGlobalsAndStillEmits()
    {
        var input=Write("lib-with-no-lib.ts","declare function log(message?: any): void;\ndeclare const aNumber: number;\nfor (const x of [1, 2, 3]) {\n    log(x);\n}\n");
        var result=Run("--emitJs","--noLib","--lib","es5","--target","es2015",input);
        Assert.Equal(2,result.Code);
        var lines=result.Output.Replace("\r\n","\n").Split('\n',StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("error TS5053: Option 'lib' cannot be specified with option 'noLib'.",lines[0]);
        Assert.Equal(11,lines.Length);
        Assert.All(lines.Skip(1),l=>Assert.StartsWith("error TS2318: Cannot find global type '",l));
        Assert.Contains("for (const x of [1, 2, 3]) {\n    log(x);\n}",File.ReadAllText(Path.ChangeExtension(input,".js")).Replace("\r\n","\n"));
    }

    [Fact]
    public void LibWithoutNoLibIsRejectedUntilTheStandardLibraryLoads()
    {
        var input=Write("lib-only.ts","let n: number = 1;");
        var result=Run("--emitJs","--lib","es5","--target","es2015",input);
        Assert.Equal(1,result.Code);
        Assert.Contains("TSCS1002",result.Output);
    }

    [Fact]
    public void EmitsFunctionConditionsTypeErasureAndExpressionPrecedenceFromProgramAst()
    {
        var input=Write("choose.ts",
            "function choose(flag: boolean): number { if (flag && (1 < 2)) { return 3; } else { return 4; } } let answer: number = choose(true);");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        Assert.Equal("",result.Output);
        Assert.Equal(
            "\"use strict\";\n"+
            "function choose(flag) {\n"+
            "    if (flag && (1 < 2)) {\n"+
            "        return 3;\n"+
            "    } else {\n"+
            "        return 4;\n"+
            "    }\n"+
            "}\n"+
            "let answer = choose(true);\n",
            File.ReadAllText(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void EmitsSpreadOperandsWithoutAddedGroupingAndFourSpaceBlocks()
    {
        // Original TS7 compiler/noCrashOnNoLib.ts output: a parenthesized
        // binary operand gets no extra grouping, bare operands stay bare,
        // nested blocks use 4 spaces.
        var input=Write("spread.ts",
            "export function f() {\n    let e: {}[] = [];\n    while (true) {\n      e = [...(e || [])];\n      e = [...e, ...[e]];\n    }\n}\n");
        var result=Run("--emitJs","--noLib","--target","es2015","--module","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        Assert.Equal("",result.Output);
        Assert.Equal(
            "export function f() {\n"+
            "    let e = [];\n"+
            "    while (true) {\n"+
            "        e = [...(e || [])];\n"+
            "        e = [...e, ...[e]];\n"+
            "    }\n"+
            "}\n",
            File.ReadAllText(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void EmitsNestedScopesElseIfAndUpdatesWithoutReparsing()
    {
        var input=Write("branches.ts",
            "let n: number = 1; { n++; } if (n > 2) { n = 3; } else if (n == 2) { n = 4; } else { n = 5; }");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        var js=File.ReadAllText(Path.ChangeExtension(input,".js"));
        Assert.Equal("\"use strict\";\nlet n = 1;\n{\n    n++;\n}\nif (n > 2) {\n    n = 3;\n} else if (n == 2) {\n    n = 4;\n} else {\n    n = 5;\n}\n",js);
    }

    [Fact]
    public void EmitsLinkedNamedModulesWithRuntimeJsSpecifiersAndAliasReexports()
    {
        var value=Write("value.ts","export const value: number = 21;");
        var bridge=Write("bridge.ts","export { value as answer } from \"./value.js\";");
        var entry=Write("entry.ts",
            "import { answer as input } from \"./bridge.js\"; export function twice(x: number): number { return x + x; } export const result: number = twice(input);");
        var response=Run("--emitJs","--module","es2015","--noLib","--target","es2015",entry,Globals());
        Assert.Equal(0,response.Code);
        Assert.Equal("",response.Output);
        Assert.Equal("export const value = 21;\n",File.ReadAllText(Path.ChangeExtension(value,".js")));
        Assert.Equal("export { value as answer } from \"./value.js\";\n",
            File.ReadAllText(Path.ChangeExtension(bridge,".js")));
        Assert.Equal("import { answer as input } from \"./bridge.js\";\n"+
            "export function twice(x) {\n    return x + x;\n}\n"+
            "export const result = twice(input);\n",
            File.ReadAllText(Path.ChangeExtension(entry,".js")));
    }

    [Theory]
    [InlineData("export function identity(eval: number): number { return eval; }")]
    [InlineData("export function identity(arguments: number): number { return arguments; }")]
    [InlineData("export function eval(x: number): number { return x; }")]
    [InlineData("export function arguments(x: number): number { return x; }")]
    [InlineData("function identity(eval: number): number { return eval; } export { identity };")]
    public void StrictModuleFunctionBindingsAreRejectedBeforeOutput(string source)
    {
        var input=Write("strict-function.ts",source);
        var response=Run("--emitJs","--module","es2015","--noLib","--target","es2015",input,Globals());
        Assert.Equal(2,response.Code);
        Assert.Contains("TS1100",response.Output);
        Assert.False(File.Exists(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void StrictModuleImportAliasesAreRejectedBeforeAnyOutput()
    {
        var dependency=Write("strict-dependency.ts","export const value: number = 1;");
        var entry=Write("strict-consumer.ts",
            "import { value as eval } from \"./strict-dependency.js\"; export const answer: number = eval;");
        var response=Run("--emitJs","--module","es2015","--noLib","--target","es2015",entry,Globals());
        Assert.Equal(2,response.Code);
        Assert.Contains("TS1100",response.Output);
        Assert.False(File.Exists(Path.ChangeExtension(entry,".js")));
        Assert.False(File.Exists(Path.ChangeExtension(dependency,".js")));
    }

    [Fact]
    public void TypeErrorInLinkedModulePreventsAllOutput()
    {
        var dependency=Write("bad-value.ts","export const value: number = \"incorrect\";");
        var entry=Write("bad-entry.ts","import { value } from \"./bad-value.js\"; let result: number = value;");
        var response=Run("--emitJs","--module","es2015","--noLib","--target","es2015",entry,Globals());
        Assert.Equal(2,response.Code);
        Assert.Contains("TS2322",response.Output);
        Assert.False(File.Exists(Path.ChangeExtension(entry,".js")));
        Assert.False(File.Exists(Path.ChangeExtension(dependency,".js")));
    }

    [Fact]
    public void UnsupportedModuleModeDoesNotWriteOutput()
    {
        var source=Write("guard-module.ts","export const value: number = 1;");
        var response=Run("--emitJs","--module","commonjs","--noLib","--target","es2015",source,Globals());
        Assert.Equal(1,response.Code);
        Assert.Contains("Only --module es2015/es6",response.Output);
        Assert.False(File.Exists(Path.ChangeExtension(source,".js")));
    }

    [Fact]
    public void AnyTypeOrSyntaxErrorPreventsJavaScriptOutput()
    {
        var input=Write("bad.ts","let n: number = \"wrong\";");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(2,result.Code);
        Assert.Contains("TS2322",result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void EmptyStatementCannotPromoteLaterStringIntoStrictModeDirective()
    {
        var input=Write("directive.ts",
            "; \"use strict\"; function identity(value: number): number { return value; } let answer: number = identity(42);");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        // The pinned useStrict transform adds the prologue; the later string stays an expression.
        Assert.StartsWith("\"use strict\";\n;\n\"use strict\";\n",File.ReadAllText(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void LeadingUseStrictDirectiveIsTheScriptPrologue()
    {
        var input=Write("prologue.ts","'use strict';\nlet n: number = 1;");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        Assert.Equal("'use strict';\nlet n = 1;\n",File.ReadAllText(Path.ChangeExtension(input,".js")));
    }

    [Theory]
    [InlineData("function identity(eval: number): number { return eval; }","eval")]
    [InlineData("function count(arguments: number): number { return arguments; }","arguments")]
    public void ScriptParametersAreStrictLikeTheReference(string source,string name)
    {
        var input=Write("sloppy.ts",source);
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(2,result.Code);
        Assert.Contains($"error TS1100: Invalid use of '{name}' in strict mode.",result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void ModuleEmitFailsClosedWithoutCreatingFiles()
    {
        var input=Write("module.ts","export const value: number = 42;");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(1,result.Code);
        Assert.Contains("ES module JavaScript output is not implemented",result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void ExistingJavaScriptIsNeverOverwritten()
    {
        var input=Write("existing.ts","let n: number = 1;");
        var existing=Write("existing.js","user content");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(1,result.Code);
        Assert.Contains("refusing to overwrite",result.Output);
        Assert.Equal("user content",File.ReadAllText(existing));
    }

    [Fact]
    public void MultiSourceOutputIsPreflightedBeforeAnyFileIsWritten()
    {
        var first=Write("first.ts","let first: number = 1;");
        var second=Write("second.ts","let second: number = first;");
        Write("second.js","existing");
        var result=Run("--emitJs","--noLib","--target","es2015",first,second,Globals());
        Assert.Equal(1,result.Code);
        Assert.False(File.Exists(Path.ChangeExtension(first,".js")));
        Assert.Equal("existing",File.ReadAllText(Path.ChangeExtension(second,".js")));
    }

    [Theory]
    [InlineData("--noEmit --noLib --target es2015")]
    [InlineData("--noLib --target es2015 --noEmit true")]
    [InlineData("--target es2015")]
    [InlineData("--noLib")]
    public void EmitRequiresExplicitSupportedMode(string flags)
    {
        var source=Write("guards.ts","let n: number = 1;");
        var options=new[]{"--emitJs"}.Concat(flags.Split(' ',StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var result=Run([..options,source,Globals()]);
        Assert.Equal(1,result.Code);
        Assert.Contains("AST-backed output requires",result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(source,".js")));
    }

    [Fact]
    public void LongLeftAssociativeExpressionsAreEmittedIteratively()
    {
        var expression=string.Join(" + ",Enumerable.Repeat("1",3000));
        var source=Write("long.ts","const total: number = "+expression+";");
        var result=Run("--emitJs","--noLib","--target","es2015",source,Globals());
        Assert.Equal(0,result.Code);
        Assert.Equal("\"use strict\";\nconst total = "+expression+";\n",File.ReadAllText(Path.ChangeExtension(source,".js")));
    }
}
