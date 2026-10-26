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
    [InlineData("let n = 1 + 2;")]
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
    [InlineData("let n = 1; n++;")]
    [InlineData("// @ts-ignore\nlet n: number = \"x\";")]
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
