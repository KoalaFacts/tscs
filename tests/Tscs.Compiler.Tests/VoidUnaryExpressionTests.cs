using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed unary expressions, not original TS7 acceptance.
public sealed class VoidUnaryExpressionTests
{
    [Theory]
    [InlineData("let result:undefined=void 21;")]
    [InlineData("let result:void=void \"ignored\";")]
    [InlineData("let result:undefined=void void 21;")]
    [InlineData("function take(n:number):undefined{return void n;} let result:undefined=take(21);")]
    [InlineData("function bump(n:number):number{return n+1;} let ignored:undefined=void bump(21);")]
    [InlineData("let ignored:undefined=void (2 ** 3);")]
    [InlineData("let ignored:undefined=void [21,22];")]
    [InlineData("let ignored:undefined=void {value:21};")]
    [InlineData("let ignored:undefined=void (21+21);")]
    [InlineData("let ignored:undefined=void (true ? 21 : 42);")]
    public void CheckedOperandProducesUndefinedInsteadOfItsValue(string source) =>
        Assert.Empty(Compilation.Check("void-unary.ts",source));

    [Theory]
    [InlineData("let wrong:number=void 21;","TS2322")]
    [InlineData("let wrong:string=void \"ignored\";","TS2322")]
    [InlineData("let wrong:boolean=void true;","TS2322")]
    [InlineData("function wrong():number{return void 21;}","TS2322")]
    [InlineData("let result:undefined=void missing;","TS2304")]
    [InlineData("let value:undefined=void 2 ** 3;","TSCS1001")]
    public void WrongResultTypesAndInvalidOperandsAreNeverAccepted(string source,string code) =>
        Assert.Contains(Compilation.Check("void-unary-invalid.ts",source),d=>d.Code==code);

    [Fact]
    public void UndefinedAssignsToVoidButNotNumericTypes()
    {
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Undefined,CompilerTypes.Void));
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Undefined,CompilerTypes.Undefined));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Undefined,CompilerTypes.Number));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Void,CompilerTypes.Undefined));
    }
}
