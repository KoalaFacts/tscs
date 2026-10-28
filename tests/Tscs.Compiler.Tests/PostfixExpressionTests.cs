using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded executable compiler regressions, not original TS7 acceptance.
public sealed class PostfixExpressionTests
{
    [Theory]
    [InlineData("function f():number { let n:number=3; let before:number=n++; return before+n; }")]
    [InlineData("function f():number { let n:number=3; return n--; }")]
    [InlineData("function f(xs:number[]):number { let i:number=0; let previous:number=xs[i++]; return previous+i; }")]
    [InlineData("function f(xs:number[]):number { let old:number=xs[0]++; return old; }")]
    [InlineData("function f():number { let box:{count:number}={count:1}; return box.count++; }")]
    [InlineData("function f():number { let n:number=1; return n++ + n++; }")]
    [InlineData("function f():number { let n:number=1; for(let i:number=0;i<3;i++){ n++; } return n; }")]
    [InlineData("function f():number { let n:number=1; return +(n++); }")]
    [InlineData("function f(xs:number[]):number { let i:number=0; return ++xs[i++]; }")]
    public void PostfixUpdatesProducePreviousValues(string source)=>
        Assert.Empty(Compilation.Check("postfix.ts",source));

    [Theory]
    [InlineData("function f():number { const n:number=1; return n++; }","TS2588")]
    [InlineData("interface D { readonly x:number } function f(d:D):number { return d.x++; }","TS2540")]
    [InlineData("function f():number { let s:string=\"ab\"; return s[0]++; }","TS2542")]
    [InlineData("function f():number { let n:number=0; return (n+1)++; }","TSCS1001")]
    [InlineData("function f():number { let s:string=\"ab\"; return s++; }","TSCS1001")]
    [InlineData("function f():number { let n:number; return n++; }","TSCS1001")]
    [InlineData("function f():number { let n:number=1; return ++n++; }","TSCS1001")]
    public void IllegalPostfixMutationIsBlocking(string source,string diagnostic)=>
        Assert.Contains(Compilation.Check("invalid-postfix.ts",source),d=>d.Code==diagnostic);

    [Fact]
    public void StringIndexUpdateRetainsBothPinnedDiagnostics()
    {
        const string source="function f():number { let s:string=\"ab\"; return s[0]++; }";
        var codes=Compilation.Check("string-postfix.ts",source)
            .Select(d=>d.Code).OrderBy(code=>code,StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]{"TS2356","TS2542"},codes);
    }

    [Fact]
    public void LineBreakSeparatesPostfixAndFollowingPrefix()
    {
        const string source="function f():number { let a:number=1; let b:number=2; a\n++b; return a+b; }";
        Assert.Empty(Compilation.Check("newline.ts",source));
    }
}
