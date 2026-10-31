using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler behavior, independent of full original TS7 acceptance.
public sealed class UnbracedConditionalTests
{
    [Theory]
    [InlineData("function f(flag:boolean):number{if(flag)return 21;else return 0;}")]
    [InlineData("function f(n:number):number{if(n)return n;return 0;}")]
    [InlineData("function f(a:boolean,b:boolean):number{if(a)if(b)return 21;else return 10;return 0;}")]
    [InlineData("function f(n:number):number{let result:number=0;if(n)result=21;else result=0;return result;}")]
    [InlineData("function f(n:number):number{if(n)throw \"error\";return 0;}")]
    [InlineData("function f(n:number):number{if(n)return 21;else;return 0;}")]
    [InlineData("function f(a:boolean,b:boolean):number{if(a)return 1;else if(b)return 2;else return 3;}")]
    [InlineData("function f(n:number):number{if(n)\nreturn 21;\nelse\nreturn 0;}")]
    public void UnbracedArmsRetainCheckedControlFlow(string source) =>
        Assert.Empty(Compilation.Check("unbraced-conditional.ts",source));

    [Theory]
    [InlineData("function f(n:number):number{if(n);else return 5;return 0;}", "TS1313")]
    [InlineData("function f(flag:boolean):number{if(flag)return \"bad\";else return 0;}", "TS2322")]
    [InlineData("function f(flag:boolean):number{if(flag)missing;return 0;}", "TS2304")]
    [InlineData("function f(flag:boolean):number{if(flag)return 21;else return \"bad\";}", "TS2322")]
    [InlineData("function f(flag:boolean):number{if(flag)const x=1;return 0;}", "TSCS1001")]
    [InlineData("function f(flag:boolean):number{if(flag)let x=1;return 0;}", "TSCS1001")]
    [InlineData("function f(flag:boolean):number{if(flag)}", "TSCS1001")]
    public void UnbracedArmsRejectRealErrorsAndUnimplementedGrammar(string source,string code) =>
        Assert.Contains(Compilation.Check("unbraced-conditional.ts",source),d=>d.Code==code);
}
