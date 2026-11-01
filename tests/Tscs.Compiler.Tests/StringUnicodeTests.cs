using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed checks, separate from original TS7 harness acceptance.
public sealed class StringUnicodeTests
{
    [Theory]
    [InlineData("let s:string=\"  ab \".trimStart();")]
    [InlineData("let s:string=\" ab  \".trimEnd();")]
    [InlineData("let s:string=\"7\".padStart(3,\"0\");")]
    [InlineData("let s:string=\"7\".padEnd(3,\".\");")]
    [InlineData("let s:string=\"x\".padStart(3);")]
    [InlineData("let s:string=\"x\".padEnd(3,undefined);")]
    [InlineData("let s:string=\"café\".normalize();")]
    [InlineData("let s:string=\"café\".normalize(\"NFD\");")]
    [InlineData("let n:number|undefined=\"Ab\".codePointAt(0);")]
    [InlineData("let n:number=\"hello\".localeCompare(\"hello\");")]
    [InlineData("let s:string=\" X \".trimStart().trimEnd().padStart(3,\"0\");")]
    [InlineData("let n:number=\"Aa\".normalize(\"NFC\").localeCompare(\"Aa\");")]
    [InlineData("let s:\"x\"|\"y\"=\"x\";let p:string=s.padStart(2,\"0\");")]
    [InlineData("let n:number|undefined=\"😀\".codePointAt(0);")]
    public void NativeUnicodeMethodPrograms(string source) =>
        Assert.Empty(Compilation.Check("string-unicode.ts",source));

    [Theory]
    [InlineData("let s:string=\"x\";s.trimStart(1);", "TS2554")]
    [InlineData("let s:string=\"x\";s.trimEnd(\"x\");", "TS2554")]
    [InlineData("let s:string=\"x\";s.padStart();", "TS2554")]
    [InlineData("let s:string=\"x\";s.padEnd();", "TS2554")]
    [InlineData("let s:string=\"x\";s.padStart(\"3\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.padEnd(3,7);", "TS2345")]
    [InlineData("let s:string=\"x\";s.normalize(2);", "TS2345")]
    [InlineData("let s:string=\"x\";s.codePointAt();", "TS2554")]
    [InlineData("let s:string=\"x\";s.codePointAt(\"0\");", "TS2345")]
    [InlineData("let s:string=\"x\";s.localeCompare();", "TS2554")]
    [InlineData("let s:string=\"x\";s.localeCompare(2);", "TS2345")]
    [InlineData("let n:number=\"x\".trimStart();", "TS2322")]
    [InlineData("let n:number=\"x\".padStart(3);", "TS2322")]
    [InlineData("let s:string=\"x\".codePointAt(0);", "TS2322")]
    public void IncorrectUnicodeMethodsAreRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("string-unicode-bad.ts",source),d=>d.Code==code);
}
