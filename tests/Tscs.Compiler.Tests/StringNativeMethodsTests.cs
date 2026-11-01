using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed semantic checks, not full original TS7 acceptance.
public sealed class StringNativeMethodsTests
{
    [Theory]
    [InlineData("let s:string=\"hello\";let t:string=s.toUpperCase();")]
    [InlineData("let s:\"HELLO\"=\"HELLO\";let t:string=s.toLowerCase();")]
    [InlineData("let s:string=\" x \";let t:string=s.trim();")]
    [InlineData("let s:string=\"hello\";let t:string=s.slice(1);")]
    [InlineData("let s:string=\"hello\";let t:string=s.slice(1,3);")]
    [InlineData("let s:string=\"hello\";let t:string=s.slice(undefined,3);")]
    [InlineData("let s:string=\"hello\";let b:boolean=s.includes(\"ell\");")]
    [InlineData("let s:string=\"hello\";let b:boolean=s.includes(\"l\",3);")]
    [InlineData("let s:string=\"hello\";let b:boolean=s.startsWith(\"he\");")]
    [InlineData("let s:string=\"hello\";let b:boolean=s.startsWith(\"e\",1);")]
    [InlineData("let s:string=\"hello\";let b:boolean=s.endsWith(\"lo\");")]
    [InlineData("let s:string=\"hello\";let b:boolean=s.trim().toUpperCase().slice(0,2).endsWith(\"E\");")]
    public void NativeStringCallsHaveCheckedTypes(string source) =>
        Assert.Empty(Compilation.Check("string-methods.ts",source));

    [Theory]
    [InlineData("let s:string=\"x\";s.toUpperCase(1);","TS2554")]
    [InlineData("let s:string=\"x\";s.toLowerCase(\"q\");","TS2554")]
    [InlineData("let s:string=\"x\";s.trim(0);","TS2554")]
    [InlineData("let s:string=\"x\";s.slice(\"q\");","TS2345")]
    [InlineData("let s:string=\"x\";s.slice(0,\"q\");","TS2345")]
    [InlineData("let s:string=\"x\";s.includes(5);","TS2345")]
    [InlineData("let s:string=\"x\";s.startsWith();","TS2554")]
    [InlineData("let s:string=\"x\";s.endsWith(\"x\",\"q\");","TS2345")]
    [InlineData("let n:number=\"x\".trim();","TS2322")]
    [InlineData("let t:string=\"x\".includes(\"x\");","TS2322")]
    [InlineData("let s:string=\"x\";s.slice(0,1,2);","TS2554")]
    [InlineData("let s:string=\"x\";s.includes(\"a\",0,1);","TS2554")]
    public void InvalidStringCallsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("bad-string-methods.ts",source),d=>d.Code==code);
}
