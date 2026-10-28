using Tscs;
using Xunit;

namespace Tscs.Tests;

// Checked compiler cases, not original TS7 acceptance.
public sealed class PrefixUpdateTests
{
    [Theory]
    [InlineData("function f():number { let n:number=1; let result:number=++n; return result; }")]
    [InlineData("function f():number { let n:number=2; return --n; }")]
    [InlineData("function f(xs:number[]):number { let index:number=-1; return ++xs[++index]; }")]
    [InlineData("function f():number { let item:{value:number}={value:21}; return ++item.value; }")]
    [InlineData("function f():number { let n:number=0; for(let i:number=0;i<3;++i){ ++n; } return n; }")]
    [InlineData("function f():number { let n:number=1; return -++n; }")]
    [InlineData("function f():number { let n:number=0; let a:number=++n + ++n; return a; }")]
    public void MutableNumericPrefixesCompile(string source)=>
        Assert.Empty(Compilation.Check("prefix.ts",source));

    [Theory]
    [InlineData("function f():number { const n:number=1; ++n; return n; }","TS2588")]
    [InlineData("function f():number { const n:number=1; --n; return n; }","TS2588")]
    [InlineData("interface Data { readonly n:number } function f():number { let d:Data={n:1}; ++d.n; return 0; }","TS2540")]
    [InlineData("function f():number { let s:string=\"x\"; ++s; return 0; }","TSCS1001")]
    [InlineData("function f():number { let s:string=\"x\"; ++s[0]; return 0; }","TS2542")]
    [InlineData("function f():number { let n:number=1; ++(n+1); return n; }","TSCS1001")]
    [InlineData("function f():number { let n:number; ++n; return 0; }","TSCS1001")]
    public void IllegalPrefixUpdatesAreDiagnosed(string source,string code)=>
        Assert.Contains(Compilation.Check("invalid-prefix.ts",source),d=>d.Code==code);
}
