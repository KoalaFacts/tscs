using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler tests, not original TS7 acceptance.
public sealed class StaticComputedObjectKeyTests
{
    [Theory]
    [InlineData("""const op={["twice"](n:number):number{return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""const op={["answer"]:42};const result:number=op.answer;""")]
    [InlineData("""const op={[0](n:number):number{return n+n;}};const result:number=op[0](21);""")]
    [InlineData("""const op={["\u0074wice"](n:number):number{return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""interface Op{twice(n:number):number}const op:Op={["twice"](n:number):number{return n+n;}};const result:number=op.twice(21);""")]
    [InlineData("""const op={inner:{["twice"](n:number){return n+n;}}};const result:number=op.inner.twice(21);""")]
    [InlineData("""const op={["twice"](n:number=21):number{return n+n;}};const result:number=op.twice();""")]
    [InlineData("""const op={["twice"](n:number){return n+n;}};const cb:(n:number)=>number=op.twice;const result:number=cb(21);""")]
    [InlineData("""const op={[0]:42};const result:number=op[0];""")]
    [InlineData("""const op={["twice"]:(n:number)=>n+n};const result:number=op.twice(21);""")]
    public void LiteralComputedKeysCompileThroughRealChecker(string source) =>
        Assert.Empty(Compilation.Check("computed-keys.ts",source));

    [Theory]
    [InlineData("""const op={["run"](n:number):number{return "bad";}};""","TS2322")]
    [InlineData("""const op={["run"](n:number):number{return n;}};op.run("bad");""","TS2345")]
    [InlineData("""const op={["run"](n:number):number{return n;}};op.run();""","TS2554")]
    [InlineData("""const op={["run"](n:number):number{return missingName;}};""","TS2304")]
    [InlineData("""const key="run";const op={[key](n:number):number{return n;}};""","TSCS1001")]
    [InlineData("""const op={["x"]:1,x:2};""","TS1117")]
    [InlineData("""const op={[0.5]:42};""","TSCS1001")]
    [InlineData("""const op={["x"]};""","TSCS1001")]
    public void InvalidComputedKeysAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("computed-keys-bad.ts",source),d=>d.Code==code);
}
