using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectSpreadsTests
{
    [Theory]
    [InlineData("let base:{value:number;label:string}={value:21,label:\"ok\"}; let merged:{value:number;label:string}={...base,value:42};")]
    [InlineData("let x={...{value:20},...{value:22}}; let n:number=x.value;")]
    [InlineData("let x={...{nested:{value:21}}}; let n:number=x.nested.value;")]
    [InlineData("let x={...{value:20},label:\"ok\"}; let n:number=x.value; let s:string=x.label;")]
    [InlineData("let x={...{value:20},value:42}; x.value=21;")]
    [InlineData("let x:{value:number}={...{value:21,extra:true}};")]
    [InlineData("let x={...{},value:42}; let n:number=x.value;")]
    [InlineData("function box():{value:number}{return {value:21};} let x={...box(),value:42};")]
    [InlineData("let x={...{value:21},...{label:\"ok\"}}; let n:number=x.value;")]
    public void CheckedSpreadsInferOrderedStructuralProperties(string source)=>
        Assert.Empty(Compilation.Check("object-spreads.ts",source));

    [Theory]
    [InlineData("let x={...42};","TSCS1001")]
    [InlineData("let x={...true};","TSCS1001")]
    [InlineData("let x={...\"bad\"};","TSCS1001")]
    [InlineData("let x={...[1,2]};","TSCS1001")]
    [InlineData("let x={...missing};","TS2304")]
    [InlineData("let x:{value:number}={...{value:\"bad\"}};","TS2322")]
    [InlineData("let x:{value:number}={...{value:20},value:\"bad\"};","TS2322")]
    [InlineData("let x:{value:number}={...{value:21},extra:true};","TS2353")]
    [InlineData("let x={...};","TSCS1001")]
    public void InvalidObjectSpreadsNeverCountAsImplemented(string source,string code)=>
        Assert.Contains(Compilation.Check("object-spreads.ts",source),d=>d.Code==code);
}
