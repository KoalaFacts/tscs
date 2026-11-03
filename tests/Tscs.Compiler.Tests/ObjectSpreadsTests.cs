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
    [InlineData("let x={...};","TS1109")]
    public void InvalidObjectSpreadsNeverCountAsImplemented(string source,string code)=>
        Assert.Contains(Compilation.Check("object-spreads.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let overlay:{n?:string}={};let v={n:21,...overlay};let x:number|string=v.n;")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let x:number|string|undefined=v.n;")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={n:true,...a,...b};let v2={...v,n:false};let x:boolean=v2.n;")]
    [InlineData("let first:{n?:number}={};let last:{n?:string;tag?:boolean}={};let v={n:true,...first,...last};let x:boolean|number|string=v.n;let t:boolean|undefined=v.tag;")]
    [InlineData("type Patch={n?:string};function merge(v:Patch):{n:number|string}{return {n:21,...v};}let x:number|string=merge({n:\"a\"}).n;")]
    [InlineData("let patch:{n?:string}={};let v={inner:{n:21,...patch}};let x:number|string=v.inner.n;")]
    [InlineData("let a:{n?:number}={};let b:{n?:number}={};let v={n:21,...a,...b};let x:number=v.n;")]
    [InlineData("type Item={score:number};let patch:{item?:Item}={};let v={item:{score:21},...patch};let x:number=v.item.score;")]
    [InlineData("let patch:{n?:string|undefined}={};let v={n:21,...patch};let x:number|string=v.n;")]
    public void OptionalObjectSpreadOverlapPreservesPreviousValues(string source) =>
        Assert.Empty(Compilation.Check("optional-spread-overlap.ts",source));

    [Theory]
    [InlineData("let overlay:{n?:string}={};let v={n:21,...overlay};let bad:number=v.n;", "TS2322")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let bad:boolean=v.n;", "TS2322")]
    [InlineData("let patch:{n?:number}={};let v={n:\"ok\",...patch};let bad:string=v.n;", "TS2322")]
    [InlineData("let overlay:{n?:string}={};let v={n:21,...overlay};v.n=true;", "TS2322")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let bad:{n:number|string}=v;", "TS2322")]
    [InlineData("let patch:{n?:string}={};let v={...patch,n:21};let bad:string=v.n;", "TS2322")]
    [InlineData("let patch:{n?:string}={};let v={outer:{n:21,...patch}};let bad:number=v.outer.n;", "TS2322")]
    [InlineData("let a:{n?:number}={};let b:{n?:string}={};let v={...a,...b};let bad:{n?:boolean}=v;", "TS2322")]
    [InlineData("let patch:{n?:string}={n:\"bad\"};let v:{n:21}={n:21,...patch};", "TS2322")]
    public void OptionalObjectSpreadOverlapRejectsIncompatibleTypes(string source,string expected) =>
        Assert.Equal(new[] { expected },Compilation.Check("optional-spread-overlap-invalid.ts",source)
            .Select(d=>d.Code).ToArray());

    // Unsupported conditional readonly/keyof identity must not silently pass.
    [Theory]
    [InlineData("let patch:{n?:string}={};let v={n:21,...patch} as const;")]
    [InlineData("let patch:{0?:string}={};let v={\"0\":21,...patch};")]
    [InlineData("let patch:{a?:{readonly n:number}}={};let v={a:{n:1},...patch};v.a.n=2;")]
    public void UnmodeledOptionalSpreadIdentitiesFailClosed(string source) =>
        Assert.Contains(Compilation.Check("optional-spread-unmodeled.ts",source),
            d=>d.Code=="TSCS1001");
}
