using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectShorthandTests
{
    [Theory]
    [InlineData("let value=21;let x={value};let n:number=x.value;")]
    [InlineData("let value=21;let label=\"ok\";let x={value,label};let s:string=x.label;")]
    [InlineData("function box(value:number):{value:number}{return {value};}let n:number=box(42).value;")]
    [InlineData("let value=21;let x={nested:{value}};let n:number=x.nested.value;")]
    [InlineData("let value=21;let x={value,...{other:1}};let n:number=x.value;")]
    [InlineData("let value=21;let x={...{value:0},value};let n:number=x.value;")]
    [InlineData("let value=21;let x:{value:number}={value};")]
    [InlineData("let value=21;let x={value,other:value+1};let n:number=x.other;")]
    public void ShorthandValuesReuseLexicalBindingsAndStructuralTypes(string source)=>
        Assert.Empty(Compilation.Check("shorthand.ts",source));

    [Theory]
    [InlineData("let x={missing};","TS18004")]
    [InlineData("let value=21;let x={value,value};","TS1117")]
    [InlineData("let value=\"bad\";let x:{value:number}={value};","TS2322")]
    [InlineData("let value=21;let x:{value:number}={value,extra:1};","TS2353")]
    [InlineData("let value=21;let x={\"value\"};","TSCS1001")]
    [InlineData("let value=21;let x={value()};","TSCS1001")]
    [InlineData("let value=21;let x={return};","TSCS1001")]
    public void MissingOrInvalidShorthandCannotPassAsImplemented(string source,string code)=>
        Assert.Contains(Compilation.Check("shorthand.ts",source),d=>d.Code==code);

    [Fact]
    public void MissingShorthandDiagnosticPointsToItsName()
    {
        const string source="const result={missing};";
        var diagnostic=Assert.Single(Compilation.Check("shorthand.ts",source));
        Assert.Equal("TS18004",diagnostic.Code);
        Assert.Equal(source.IndexOf("missing",StringComparison.Ordinal),diagnostic.Start);
        Assert.Equal(7,diagnostic.Length);
    }
}
