using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded product regressions, not original TS7 acceptance.
public sealed class UnbracedLoopTests
{
    [Theory]
    [InlineData("function f(n:number):number{while(n>0)n--;return n;}")]
    [InlineData("function f(n:number):number{let i:number=0;do i++;while(i<n);return i;}")]
    [InlineData("function f(n:number):number{let sum:number=0;for(let i=0;i<n;i++)sum+=i;return sum;}")]
    [InlineData("function f(values:number[]):number{let sum:number=0;for(const x of values)sum+=x;return sum;}")]
    [InlineData("function f(data:{a:number;b:number}):number{let count:number=0;for(const key in data)count++;return count;}")]
    [InlineData("function f(n:number):number{while(n>0)if(n===2)break;else n--;return n;}")]
    [InlineData("function f(n:number):number{do if(n===1)break;else n--;while(n>0);return n;}")]
    [InlineData("function f(n:number):number{let sum:number=0;for(let i=0;i<n;i++)if(i>2)break;else sum+=i;return sum;}")]
    [InlineData("function f(values:number[]):number{let sum:number=0;for(const x of values)if(x<0)continue;else sum+=x;return sum;}")]
    [InlineData("function f(data:{a:number;b:number}):number{let total:number=0;for(const key in data)if(key===\"a\")total++;else total+=2;return total;}")]
    [InlineData("function f():number{while(false);for(let i=0;i<0;i++);return 42;}")]
    [InlineData("function f(n:number):number{while(n>0)return n;return 0;}")]
    public void SingleStatementLoopBodiesReuseCheckedPipeline(string source) =>
        Assert.Empty(Compilation.Check("unbraced-loops.ts",source));

    [Theory]
    [InlineData("function f(n:number):number{while(n>0)n=\"bad\";return n;}", "TS2322")]
    [InlineData("function f(n:number):number{do n=\"bad\";while(n>0);return n;}", "TS2322")]
    [InlineData("function f(n:number):number{let total:number=0;for(let i=0;i<n;i++)total=\"bad\";return total;}", "TS2322")]
    [InlineData("function f(values:number[]):number{let total:number=0;for(const x of values)total=\"bad\";return total;}", "TS2322")]
    [InlineData("function f(data:{a:number}):number{let total:number=0;for(const key in data)total=\"bad\";return total;}", "TS2322")]
    [InlineData("function f(n:number):number{while(n>0)let item=1;return n;}", "TSCS1001")]
    [InlineData("function f(n:number):number{do const item=1;while(n>0);return n;}", "TSCS1001")]
    [InlineData("function f(n:number):number{for(let i=0;i<n;i++)const x=1;return n;}", "TSCS1001")]
    public void SemanticErrorsAndUnsupportedUnbracedLexicalDeclarationsBlock(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("unbraced-loops-invalid.ts",source),d=>d.Code==diagnostic);

    [Fact]
    public void LoopBodyCannotProvePostLoopInitialization() =>
        Assert.Contains(Compilation.Check("unbraced-loop-flow.ts",
            "function f(n:number):number{let value:number;while(n>0)value=42;return value;}"),
            d=>d.Code=="TSCS1001");
}
