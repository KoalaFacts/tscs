using Tscs;
using Xunit;

namespace Tscs.Tests;

// Compiler behavior only; not original TypeScript 7 acceptance.
public sealed class StringIterationTests
{
    [Theory]
    [InlineData("let text:string=\"ab\";let out:string=\"\";for(const ch of text){out=out+ch;}")]
    [InlineData("let count:number=0;for(const ch of \"a😀b\"){count++;}")]
    [InlineData("let out:string=\"\";for(let ch of \"abc\"){out=out+ch;}")]
    [InlineData("function collect(s:string):string{let out:string=\"\";for(const ch of s){out=out+ch;}return out;}let r:string=collect(\"abc\");")]
    [InlineData("let words:string[]=[\"ab\",\"cd\"];for(const word of words){for(const ch of word){let copy:string=ch;}}")]
    [InlineData("let s:\"x\"=\"x\";for(const ch of s){let copy:string=ch;}")]
    [InlineData("let s:string=\"abc\";for(const ch of s){if(ch===\"b\"){continue;}if(ch===\"c\"){break;}}")]
    public void StringsAreCheckedAsNativeEs2015Iterables(string source) =>
        Assert.Empty(Compilation.Check("string-forof.ts",source));

    [Theory]
    [InlineData("for(const ch of \"ab\"){ch=\"x\";}","TS2588")]
    [InlineData("for(const ch of \"ab\"){let bad:number=ch;}","TS2322")]
    [InlineData("let s:string=\"ab\";for(let ch of s){ch=42;}","TS2322")]
    [InlineData("for(const ch of \"ab\"){ch.missing;}","TS2339")]
    [InlineData("for(const ch of 42){let copy:number=ch;}","TSCS1001")]
    [InlineData("function invalid(s:string|undefined):number{for(const ch of s){return 1;}return 0;}","TSCS1001")]
    public void BadIterationAndUsesDoNotSucceed(string source,string code) =>
        Assert.Contains(Compilation.Check("string-forof-negative.ts",source),d=>d.Code==code);
}
