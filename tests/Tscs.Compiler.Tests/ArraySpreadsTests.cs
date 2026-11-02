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

    // Bounded tuple-value spreads copy exact checked positions rather than
    // widening a spread tuple to its joined array element type.
    [Theory]
    [InlineData("let tail:[string,boolean]=[\"go\",true];let p:[number,string,boolean]=[21,...tail];let s:string=p[1];")]
    [InlineData("type Tail=[string,boolean];let tail:Tail=[\"go\",true];let p:[number,...Tail]=[21,...tail];")]
    [InlineData("let tail:readonly [string,boolean]=[\"go\",true];let p:[number,string,boolean]=[21,...tail];")]
    [InlineData("let p:[number,string,boolean]=[21,...([\"go\",true] as const)];")]
    [InlineData("let a:[number]=[21];let b:[string,boolean]=[\"go\",true];let p:[number,string,boolean]=[...a,...b];")]
    [InlineData("function make(t:[string,boolean]):[number,string,boolean]{return [21,...t];}let p=make([\"go\",true]);")]
    [InlineData("let t:[string]=[\"go\"];let p:[number,string?]=[21,...t];")]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:readonly [number,string,boolean]=[21,...t];")]
    public void FiniteValueTupleSpreadsRetainCheckedPositions(string source) =>
        Assert.Empty(Compilation.Check("finite-value-spread.ts",source));

    [Theory]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:[number,number,boolean]=[21,...t];","TS2322")]
    [InlineData("let t:[string]=[\"go\"];let p:[number,string,boolean]=[21,...t];","TS2322")]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:[number,string]=[21,...t];","TS2322")]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:[number,string,boolean]=[...t];","TS2322")]
    [InlineData("let t:number[]=[1,2];let p:[number,number,number]=[21,...t];","TS2322")]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:[number,string,boolean]=[21,...t];p[1]=21;","TS2322")]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:readonly [number,string,boolean]=[21,...t];p[1]=\"no\";","TS2540")]
    [InlineData("let t:[string,boolean]=[\"go\",true];let p:[number,string,boolean]=[21,...t];let bad=p[3];","TS2493")]
    public void FiniteValueTupleSpreadsRejectWrongArityAndSlots(string source,string code) =>
        Assert.Contains(Compilation.Check("finite-value-spread-invalid.ts",source),d=>d.Code==code);
}
