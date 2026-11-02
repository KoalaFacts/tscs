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

    // An optional-length tuple may be spread only at the tail of a
    // contextually checked finite tuple. Both possible length bounds must fit.
    [Theory]
    [InlineData("let t:[string?]=[];let p:[number,string?]=[21,...t];let n:1|2=p.length;")]
    [InlineData("let t:[string?]=[\"go\"];let p:readonly [number,string?]=[21,...t];let v:string|undefined=p[1];")]
    [InlineData("type Tail=[string,boolean?];let t:Tail=[\"go\"];let p:[number,...Tail]=[21,...t];")]
    [InlineData("let t:[string?,boolean?]=[];let p:[number,string?,boolean?]=[21,...t];")]
    [InlineData("let t:readonly [string,boolean?]=[\"go\",true];let p:[number,string,boolean?]=[21,...t];")]
    [InlineData("function make(t:[string?]):[number,string?]{return [21,...t];}let p=make([\"go\"]);")]
    [InlineData("let t:[string?]=[];let p:[number,...[string?]]=[21,...t];")]
    [InlineData("let t:[string,boolean?]=[\"go\"];let p:[number,string,boolean?]=[21,...t];let s:string=p[1];")]
    public void TrailingOptionalTupleSpreadsKeepFiniteLengthAndSlots(string source) =>
        Assert.Empty(Compilation.Check("optional-tuple-spread.ts",source));

    [Theory]
    [InlineData("let t:[string?]=[];let p:[number,string]=[21,...t];","TS2322")]
    [InlineData("let t:[string?,boolean?]=[];let p:[number,string?]=[21,...t];","TS2322")]
    [InlineData("let t:[string?,boolean?]=[];let p:[number,boolean?,boolean?]=[21,...t];","TS2322")]
    [InlineData("let t:[string,boolean?]=[\"go\"];let p:[number,string,boolean]=[21,...t];","TS2322")]
    [InlineData("let t:[string?]=[];let p:[number,string?]=[21,...t];p[1]=21;","TS2322")]
    [InlineData("let t:[string?]=[];let p:[number,string?]=[21,...t];let bad=p[2];","TS2493")]
    [InlineData("let t:[string?]=[];let p:[number,string?]=[\"bad\",...t];","TS2322")]
    [InlineData("function make(t:[string?]):[number,string]{return [21,...t];}","TS2322")]
    public void TrailingOptionalTupleSpreadsRejectInvalidRangeOrSlots(string source,string code) =>
        Assert.Contains(Compilation.Check("optional-tuple-spread-invalid.ts",source),d=>d.Code==code);

    [Fact]
    public void NonterminalOptionalSpreadCannotPretendToBeFixedLength()
    {
        const string source="let t:[string?]=[];let p:[number,string?,boolean?]=[21,...t,true];";
        Assert.Contains(Compilation.Check("optional-tuple-nonterminal.ts",source),
            d=>d.Code=="TS2322");
    }
}
