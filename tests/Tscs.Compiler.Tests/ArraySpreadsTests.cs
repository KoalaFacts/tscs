using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ArraySpreadsTests
{
    [Theory]
    [InlineData("let xs:number[]=[20,21]; let ys:number[]=[...xs,1];")]
    [InlineData("let xs:number[]=[1]; let ys:number[]=[...xs,20,...xs];")]
    [InlineData("let xs:string[]=[\"a\"]; let ys:string[]=[\"b\",...xs];")]
    [InlineData("let xs:{value:number}[]=[{value:21}]; let ys:{value:number}[]=[...xs,{value:21}];")]
    [InlineData("let xs:number[][]=[[1],[2]]; let ys:number[][]=[...xs];")]
    [InlineData("let xs:number[]=[1]; let ys:number[]=[...[...xs]];")]
    [InlineData("let xs:number[]=[1]; let ys=[...xs,2]; let first:number=ys[0];")]
    [InlineData("function f(xs:number[]):number{return [...xs,1].length;} let n:number=f([1,2]);")]
    [InlineData("let xs:number[]=[21]; let ys:number[]=[...xs,...[21]];")]
    public void CheckedArraySpreadPassesThroughParserBinderChecker(string source) =>
        Assert.Empty(Compilation.Check("spread.ts",source));

    [Theory]
    [InlineData("let xs:number[]=[...42];","TSCS1001")]
    [InlineData("let xs:number[]=[...true];","TSCS1001")]
    [InlineData("let xs:number[]=[...{value:21}];","TSCS1001")]
    [InlineData("let xs:number[]=[...\"bad\"];","TSCS1001")]
    [InlineData("let xs:number[]=[...\"bad\",21];","TSCS1001")]
    [InlineData("let xs:number[]=[...[\"bad\"]];","TS2322")]
    [InlineData("let xs:string[]=[\"bad\"]; let ys:number[]=[...xs];","TS2322")]
    [InlineData("let xs:number[]=[1]; let ys:number[]=[...xs,...missing];","TS2304")]
    [InlineData("let xs:number[]=[,...[1]];","TSCS1001")]
    [InlineData("let xs:number[]=[...];","TSCS1001")]
    public void NonArrayOrIncompatibleSpreadCannotSucceed(string source,string code) =>
        Assert.Contains(Compilation.Check("spread.ts",source),d=>d.Code==code);
}
