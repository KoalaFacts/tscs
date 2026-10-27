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
    public void EmitsFunctionConditionsTypeErasureAndExpressionPrecedenceFromProgramAst()
    {
        var input=Write("choose.ts",
            "function choose(flag: boolean): number { if (flag && (1 < 2)) { return 3; } else { return 4; } } let answer: number = choose(true);");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        Assert.Equal("",result.Output);
        Assert.Equal(
            "function choose(flag) {\n"+
            "  if (flag && (1 < 2)) {\n"+
            "    return 3;\n"+
            "  } else {\n"+
            "    return 4;\n"+
            "  }\n"+
            "}\n"+
            "let answer = choose(true);\n",
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
        Assert.Equal("let n = 1;\n{\n  n++;\n}\nif (n > 2) {\n  n = 3;\n} else if (n == 2) {\n  n = 4;\n} else {\n  n = 5;\n}\n",js);
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
            "; \"use strict\"; function identity(eval: number): number { return eval; } let answer: number = identity(42);");
        var result=Run("--emitJs","--noLib","--target","es2015",input,Globals());
        Assert.Equal(0,result.Code);
        Assert.StartsWith(";\n\"use strict\";\n",File.ReadAllText(Path.ChangeExtension(input,".js")));
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
        Assert.Equal("const total = "+expression+";\n",File.ReadAllText(Path.ChangeExtension(source,".js")));
    }
}
