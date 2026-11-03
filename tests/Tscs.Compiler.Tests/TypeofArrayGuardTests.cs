using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypeofArrayGuardTests
{
    [Theory]
    [InlineData("function f(v:number[]|string):void{if(typeof v===\"object\"){let a:number[]=v;}}")]
    [InlineData("function f(v:number[]|string):void{if(typeof v===\"object\"){}else{let a:string=v;}}")]
    [InlineData("function f(v:number[]|string):void{if(typeof v!==\"object\"){let a:string=v;}else{v.push(21);}}")]
    [InlineData("function f(v:[number,string]|string):void{if(typeof v===\"object\"){let a:[number,string]=v;}}")]
    [InlineData("function f(v:readonly number[]|string):void{if(typeof v===\"object\"){let a:readonly number[]=v;}}")]
    [InlineData("function f(v:number[]|string|undefined):void{if(typeof v===\"object\"){let a:number[]=v;}else{let b:string|undefined=v;}}")]
    [InlineData("function f(v:number[]|null|string):void{if(typeof v===\"object\"){if(v!==null){let a:number[]=v;}}}")]
    [InlineData("function f(v:number[]|number):void{if(typeof v===\"object\"){let a:number[]=v;}else{let n:number=v;}}")]
    public void RuntimeTypeofArrayAndTupleGuardsNarrowOnlyKnownAlternatives(string source) =>
        Assert.Empty(Compilation.Check("typeof-array.ts",source));

    [Theory]
    [InlineData("function f(v:number[]|string):void{if(typeof v===\"object\"){let bad:string=v;}}","TS2322")]
    [InlineData("function f(v:number[]|string):void{if(typeof v===\"object\"){}else{let bad:number[]=v;}}","TS2322")]
    [InlineData("function f(v:[number,string]|string):void{if(typeof v===\"object\"){let bad:string=v;}}","TS2322")]
    [InlineData("function f(v:readonly number[]|string):void{if(typeof v===\"object\"){v.push(21);}}","TS2339")]
    [InlineData("function f(v:number[]|null|string):void{if(typeof v===\"object\"){let bad:number[]=v;}}","TS2322")]
    [InlineData("function f(v:number[]|number):void{if(typeof v!==\"object\"){let bad:string=v;}}","TS2322")]
    [InlineData("function f(v:[number,string]|string):void{if(typeof v===\"object\"){let bad:[number,number]=v;}}","TS2322")]
    [InlineData("function f(v:number[]|string):void{if(typeof v!==\"object\"){let bad:number[]=v;}}","TS2322")]
    public void RuntimeTypeofArrayGuardsRejectUnsafeTypesAndReadonlyWrites(string source,string code) =>
        Assert.Equal(new[] {code},Compilation.Check("typeof-array-invalid.ts",source)
            .Select(d=>d.Code).ToArray());
}
