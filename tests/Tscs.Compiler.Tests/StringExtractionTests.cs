using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed semantic checks, not full original TS7 acceptance.
public sealed class StringExtractionTests
{
    [Theory]
    [InlineData("let parts:string[]=\"a,b,c\".split(\",\");")]
    [InlineData("let first:string=\"a,b\".split(\",\")[0];")]
    [InlineData("let code:number=\"abc\".charCodeAt(0);")]
    [InlineData("let ch:string=\"abc\".charAt(1);")]
    [InlineData("let part:string=\"abcd\".substring(1,3);")]
    [InlineData("let part:string=\"abcd\".substring(1);")]
    [InlineData("let pos:number=\"hello\".indexOf(\"e\");")]
    [InlineData("let pos:number=\"hello\".lastIndexOf(\"l\",3);")]
    [InlineData("let s:string=\"hello\".replace(\"ll\",\"yy\");")]
    [InlineData("let s:string=\"ab\".repeat(3);")]
    [InlineData("let parts:string[]=\"x:y\".replace(\":\",\"|\").split(\"|\");")]
    [InlineData("let joined:string=\"a,b\".split(\",\").join(\"-\");")]
    [InlineData("let s:string=\"hello\".charAt(1).repeat(2);")]
    [InlineData("let pos:number=\"hello\".indexOf(\"l\",2);")]
    public void CheckedNativeExtractionPrograms(string source) =>
        Assert.Empty(Compilation.Check("string-extraction.ts",source));

    [Theory]
    [InlineData("let s:string=\"x\";s.charAt();", "TS2554")]
    [InlineData("let s:string=\"x\";s.charAt(\"0\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.charCodeAt(\"0\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.substring();", "TS2554")]
    [InlineData("let s:string=\"x\";s.substring(\"a\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.indexOf(2);", "TS2345")]
    [InlineData("let s:string=\"x\";s.lastIndexOf(\"x\",\"2\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.replace(\"a\",2);", "TS2345")]
    [InlineData("let s:string=\"x\";s.split(2);", "TS2345")]
    [InlineData("let s:string=\"x\";s.split(\",\",\"2\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.repeat(\"2\");", "TS2345")]
    [InlineData("let n:number=\"x\".replace(\"x\",\"y\");", "TS2322")]
    [InlineData("let s:string=\"x\".split(\",\");", "TS2322")]
    [InlineData("let n:number=\"x\".charAt(0);", "TS2322")]
    public void IllTypedExtractionDoesNotSucceed(string source,string code) =>
        Assert.Contains(Compilation.Check("string-extraction-bad.ts",source),d=>d.Code==code);
}
