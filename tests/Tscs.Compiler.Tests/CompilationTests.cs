using Xunit;
using Tscs;
namespace Tscs.Tests;
public class CompilationTests
{
    [Theory]
    [InlineData("let n: number = 1;")]
    [InlineData("const s: string = \"ok\";")]
    [InlineData("let b: boolean = true;")]
    [InlineData("let x: number = 1; { let x: string = \"ok\"; } x = 2;")]
    [InlineData("let n: any = 1;")]
    [InlineData("let n = 1; n++;")]
    [InlineData("let n = 1 + 2;")]
    [InlineData("// @ts-ignore\nlet n: number = \"x\";")]
    public void ValidPrimitiveCodeHasNoErrors(string source) => Assert.Empty(Compilation.Check("test.ts", source));
    [Theory]
    [InlineData("let n: number = \"x\";", "TS2322", 4, 1)]
    [InlineData("missing;", "TS2304", 0, 7)]
    [InlineData("const n = 1; n = 2;", "TS2588", 13, 1)]
    [InlineData("{ let x: number = 1; } x;", "TS2304", 23, 1)]
    [InlineData("/* 😀 */ let n: number = \"x\";", "TS2322", 13, 1)]
    [InlineData("let n = ;", "TS1109", 8, 1)]
    [InlineData("const n: number;", "TS1155", 6, 1)]
    public void ReportsActualSourceLocations(string source,string code,int start,int length)
    {
        var d=Compilation.Check("test.ts",source).Single();
        Assert.Multiple(()=>Assert.Equal(code,d.Code),()=>Assert.Equal(start,d.Start),()=>Assert.Equal(length,d.Length));
    }
    [Theory]
    [InlineData("let n = 1; function f() {}")]
    [InlineData("const a = { x: 1 };")]
    public void UnsupportedSyntaxNeverSucceeds(string source) => Assert.Contains(Compilation.Check("test.ts",source),d=>d.Code=="TSCS1001");

    [Theory]
    [InlineData("let n: any = 1; n = \"x\";")]
    [InlineData("let n: any = \"x\"; n = false;")]
    [InlineData("let n: any = 1; let s: string = n;")]
    [InlineData("let n: number = 1; let a: any = n;")]
    [InlineData("let n: any = 1; let b: boolean = n;")]
    public void BoundedAnyVariableAssignmentsAreAssignable(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Fact]
    public void AnyAnnotatedConstantStillCannotBeReassigned() =>
        Assert.Equal("TS2588", Assert.Single(Compilation.Check("test.ts", "const n: any = 1; n = 2;")).Code);


    [Theory]
    [InlineData("let n: number = 1; n++;")]
    [InlineData("let n = 1; n++; n++;")]
    [InlineData("let n = 1; { let x: number = 2; x++; } n++;")]
    [InlineData("let n = 1; n++;\nlet m: number = n;")]
    [InlineData("let n = 1; n++")]
    [InlineData("let n = 1; n++; n = 2;")]
    public void NumericPostfixUpdatesAreWritable(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Fact]
    public void PostfixConstantUpdateReportsTs2588() =>
        Assert.Equal("TS2588", Assert.Single(Compilation.Check("test.ts", "const n = 1; n++;")).Code);

    [Theory]
    [InlineData("let s: string = \"x\"; s++;")]
    [InlineData("let b: boolean = true; b++;")]
    [InlineData("let n = 1; ++n;")]
    [InlineData("let n = 1; let m = n++;")]
    [InlineData("let n = 1; n++ + 2;")]
    public void UnsupportedPostfixBoundaryRemainsExplicit(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");

}

public class ParserBoundaryTests
{
    [Fact] public async Task UnmatchedClosingBraceTerminates()
    {
        var task=Task.Run(()=>Compilation.Check("test.ts","}"),TestContext.Current.CancellationToken);
        var completed=await Task.WhenAny(task,Task.Delay(TimeSpan.FromMilliseconds(250),TestContext.Current.CancellationToken));
        Assert.True(completed==task,"parser must always make progress");
        Assert.NotEmpty(await task);
    }
    [Theory]
    [InlineData("const return = 1;")]
    [InlineData("const case = 1;")]
    [InlineData("const extends = 1;")]
    [InlineData("let x\u0085: number = 1;")]
    public void InvalidTokensCannotSucceed(string source)=>Assert.NotEmpty(Compilation.Check("test.ts",source));
}

public class StrictBindingTests
{
    [Theory]
    [InlineData("eval")]
    [InlineData("arguments")]
    public void StrictModeInvalidBindingsAreRejected(string name)
    {
        var diagnostics=Compilation.Check("test.ts",$"let {name} = 1;");
        Assert.Contains("TS1100",diagnostics.Select(d=>d.Code));
    }
}

public class IntrinsicBindingTests
{
    [Theory]
    [InlineData("globalThis;", 0)]
    [InlineData("{ globalThis; }", 2)]
    [InlineData("{ let globalThis = 1; globalThis; } globalThis;", 36)]
    [InlineData("globalThis = 1;", 0)]
    public void IntrinsicGlobalThisIsExplicitlyUnsupported(string source,int start)
    {
        var d=Compilation.Check("test.ts",source).Single();
        Assert.Multiple(
            ()=>Assert.Equal("TSCS1001",d.Code),
            ()=>Assert.Equal("The built-in globalThis type is not supported yet.",d.Message),
            ()=>Assert.Equal(start,d.Start),
            ()=>Assert.Equal(10,d.Length));
    }
    [Theory]
    [InlineData("{ let globalThis: number = 1; globalThis; globalThis = 2; }")]
    [InlineData("{ let globalThis = 1; { globalThis; } }")]
    public void BlockLocalGlobalThisResolvesNormally(string source)=>Assert.Empty(Compilation.Check("test.ts",source));
    [Theory]
    [InlineData("{ let globalThis: number = 1; globalThis = \"x\"; }", "TS2322")]
    [InlineData("{ const globalThis = 1; globalThis = 2; }", "TS2588")]
    [InlineData("let globalThis = 1; globalThis;", "TS2397")]
    [InlineData("missing;", "TS2304")]
    [InlineData("globalthis;", "TS2304")]
    public void UserBindingsAndMissingNamesKeepTheirDiagnostics(string source,string code)=>Assert.Equal(new[]{code},Compilation.Check("test.ts",source).Select(d=>d.Code));
    [Theory]
    [InlineData("{ globalThis; let globalThis = 1; }", "References before declaration are not supported yet.")]
    [InlineData("{ let globalThis: number; globalThis; }", "Reads before initialization are not supported yet.")]
    public void BlockLocalGlobalThisKeepsInitializationGuards(string source,string message)
    {
        var d=Compilation.Check("test.ts",source).Single();
        Assert.Multiple(()=>Assert.Equal("TSCS1001",d.Code),()=>Assert.Equal(message,d.Message));
    }
    [Fact] public void GlobalThisCannotBeShadowedAtRoot()=>Assert.Contains("TS2397",Compilation.Check("test.ts","let globalThis = 1;").Select(d=>d.Code));
    [Fact] public void GlobalThisCanBeShadowedInBlock()=>Assert.Empty(Compilation.Check("test.ts","{ let globalThis = 1; }"));
}

public class TsIgnoreDirectiveTests
{
    [Theory]
    [InlineData("// @ts-ignore\nlet n: number = \"x\";")]
    [InlineData("let n: number = 1;\n// @ts-ignore\nn = \"x\";")]
    [InlineData("//@ts-ignore\r\nmissing;")]
    [InlineData("  // @ts-ignore\nmissing;")]
    [InlineData("// @ts-ignore\nconst n = 1; n = 2;")]
    public void NextLineSemanticDiagnosticsAreSuppressed(string source) =>
        Assert.Empty(Compilation.Check("test.ts", source));

    [Theory]
    [InlineData("// @ts-ignore\n\nlet n: number = \"x\";", "TS2322")]
    [InlineData("// @ts-ignore\nlet n: number = 1;\nlet m: string = 2;", "TS2322")]
    [InlineData("const s = \"// @ts-ignore\";\nlet n: number = \"x\";", "TS2322")]
    public void SuppressionIsLimitedToTheImmediateNextLine(string source, string code) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == code);

    [Fact]
    public void DirectiveDoesNotTurnSyntaxErrorsIntoSuccess() =>
        Assert.Contains(Compilation.Check("test.ts", "// @ts-ignore\nlet n = ;"), d => d.Code == "TS1109");

    [Theory]
    [InlineData("// @ts-expect-error\nlet n: number = \"x\";")]
    [InlineData("// @ts-ignoreExtra\nlet n: number = \"x\";")]
    [InlineData("/* @ts-ignore */\nlet n: number = \"x\";")]
    public void UnimplementedDirectiveVariantsRemainRejected(string source) =>
        Assert.Contains(Compilation.Check("test.ts", source), d => d.Code == "TSCS1001");
}
