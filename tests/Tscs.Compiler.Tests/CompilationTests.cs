using NUnit.Framework;
using Tscs;
namespace Tscs.Tests;
public class CompilationTests
{
    [TestCase("let n: number = 1;")]
    [TestCase("const s: string = \"ok\";")]
    [TestCase("let b: boolean = true;")]
    [TestCase("let x: number = 1; { let x: string = \"ok\"; } x = 2;")]
    public void ValidPrimitiveCodeHasNoErrors(string source) => Assert.That(Compilation.Check("test.ts", source), Is.Empty);
    [TestCase("let n: number = \"x\";", "TS2322", 4, 1)]
    [TestCase("missing;", "TS2304", 0, 7)]
    [TestCase("const n = 1; n = 2;", "TS2588", 13, 1)]
    [TestCase("{ let x: number = 1; } x;", "TS2304", 23, 1)]
    [TestCase("/* 😀 */ let n: number = \"x\";", "TS2322", 13, 1)]
    [TestCase("let n = ;", "TS1109", 8, 1)]
    [TestCase("const n: number;", "TS1155", 6, 1)]
    public void ReportsActualSourceLocations(string source,string code,int start,int length)
    {
        var d=Compilation.Check("test.ts",source).Single();
        Assert.Multiple(()=>{Assert.That(d.Code,Is.EqualTo(code));Assert.That(d.Start,Is.EqualTo(start));Assert.That(d.Length,Is.EqualTo(length));});
    }
    [TestCase("let n = 1; function f() {}")]
    [TestCase("let n = 1; n++;")]
    [TestCase("let n = 1 + 2;")]
    [TestCase("// @ts-ignore\nlet n: number = \"x\";")]
    [TestCase("let n: any = 1;")]
    [TestCase("const a = { x: 1 };")]
    public void UnsupportedSyntaxNeverSucceeds(string source) => Assert.That(Compilation.Check("test.ts",source).Any(d=>d.Code=="TSCS1001"),Is.True);
}

public class ParserBoundaryTests
{
    [Test] public void UnmatchedClosingBraceTerminates()
    {
        var task=Task.Run(()=>Compilation.Check("test.ts","}"));
        Assert.That(task.Wait(TimeSpan.FromMilliseconds(250)),Is.True,"parser must always make progress");
        Assert.That(task.Result,Is.Not.Empty);
    }
    [TestCase("const return = 1;")]
    [TestCase("const case = 1;")]
    [TestCase("const extends = 1;")]
    [TestCase("let x\u0085: number = 1;")]
    public void InvalidTokensCannotSucceed(string source)=>Assert.That(Compilation.Check("test.ts",source),Is.Not.Empty);
}

public class StrictBindingTests
{
    [TestCase("eval")]
    [TestCase("arguments")]
    public void StrictModeInvalidBindingsAreRejected(string name)
    {
        var diagnostics=Compilation.Check("test.ts",$"let {name} = 1;");
        Assert.That(diagnostics.Select(d=>d.Code),Does.Contain("TS1100"));
    }
}

public class IntrinsicBindingTests
{
    [TestCase("globalThis;", 0)]
    [TestCase("{ globalThis; }", 2)]
    [TestCase("{ let globalThis = 1; globalThis; } globalThis;", 36)]
    [TestCase("globalThis = 1;", 0)]
    public void IntrinsicGlobalThisIsExplicitlyUnsupported(string source,int start)
    {
        var d=Compilation.Check("test.ts",source).Single();
        Assert.Multiple(()=>{
            Assert.That(d.Code,Is.EqualTo("TSCS1001"));
            Assert.That(d.Message,Is.EqualTo("The built-in globalThis type is not supported yet."));
            Assert.That(d.Start,Is.EqualTo(start));
            Assert.That(d.Length,Is.EqualTo(10));
        });
    }
    [TestCase("{ let globalThis: number = 1; globalThis; globalThis = 2; }")]
    [TestCase("{ let globalThis = 1; { globalThis; } }")]
    public void BlockLocalGlobalThisResolvesNormally(string source)=>Assert.That(Compilation.Check("test.ts",source),Is.Empty);
    [TestCase("{ let globalThis: number = 1; globalThis = \"x\"; }", "TS2322")]
    [TestCase("{ const globalThis = 1; globalThis = 2; }", "TS2588")]
    [TestCase("let globalThis = 1; globalThis;", "TS2397")]
    [TestCase("missing;", "TS2304")]
    [TestCase("globalthis;", "TS2304")]
    public void UserBindingsAndMissingNamesKeepTheirDiagnostics(string source,string code)=>Assert.That(Compilation.Check("test.ts",source).Select(d=>d.Code),Is.EqualTo(new[]{code}));
    [TestCase("{ globalThis; let globalThis = 1; }", "References before declaration are not supported yet.")]
    [TestCase("{ let globalThis: number; globalThis; }", "Reads before initialization are not supported yet.")]
    public void BlockLocalGlobalThisKeepsInitializationGuards(string source,string message)
    {
        var d=Compilation.Check("test.ts",source).Single();
        Assert.Multiple(()=>{Assert.That(d.Code,Is.EqualTo("TSCS1001"));Assert.That(d.Message,Is.EqualTo(message));});
    }
    [Test] public void GlobalThisCannotBeShadowedAtRoot()=>Assert.That(Compilation.Check("test.ts","let globalThis = 1;").Select(d=>d.Code),Does.Contain("TS2397"));
    [Test] public void GlobalThisCanBeShadowedInBlock()=>Assert.That(Compilation.Check("test.ts","{ let globalThis = 1; }"),Is.Empty);
}
