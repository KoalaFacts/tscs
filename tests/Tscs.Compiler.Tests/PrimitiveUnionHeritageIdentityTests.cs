using Tscs;
using Xunit;

namespace Tscs.Tests;

// Proven concrete primitive union identities; opaque generic bindings remain
// subject to the declaration-identity checker rather than this shortcut.
public sealed class PrimitiveUnionHeritageIdentityTests
{
    [Theory]
    [InlineData("interface Base<T>{tag:true|false;value:T}interface Box<U> extends Base<U>{tag:boolean}let b:Box<number>={tag:true,value:8};")]
    [InlineData("interface Base<T>{tag:boolean;value:T}interface Box<U> extends Base<U>{tag:false|true}let b:Box<number>={tag:false,value:8};")]
    [InlineData("interface Base<T>{tag:number|1;value:T}interface Box<U> extends Base<U>{tag:number}let b:Box<number>={tag:1,value:8};")]
    [InlineData("interface Base<T>{tag:string|\\"ready\\";value:T}interface Box<U> extends Base<U>{tag:string}let b:Box<number>={tag:\\"ready\\",value:8};")]
    [InlineData("interface Base<T>{tag:never|string;value:T}interface Box<U> extends Base<U>{tag:string}let b:Box<number>={tag:\\"ok\\",value:8};")]
    [InlineData("interface Base<T>{meta:{flag:true|false;value:T}}interface Box<U> extends Base<U>{meta:{value:U;flag:boolean}}let b:Box<number>={meta:{value:8,flag:true}};")]
    [InlineData("interface Base<T>{tag:string|\\"ok\\"|string;value:T}interface Box<U> extends Base<U>{tag:string}let b:Box<number>={tag:\\"ok\\",value:8};")]
    [InlineData("type Tag=true|false;interface Base<T>{tag:Tag;value:T}interface Box<U> extends Base<U>{tag:boolean}let b:Box<number>={tag:true,value:8};")]
    public void EquivalentPrimitiveUnionHeritageCompiles(string source)=>
        Assert.Empty(Compilation.Check("primitive-union-heritage.ts",source));

    [Theory]
    [InlineData("interface Base<T>{tag:true|false;value:T}interface Box<U> extends Base<U>{tag:boolean}let b:Box<number>={tag:1,value:8};")]
    [InlineData("interface Base<T>{tag:string|\\"ready\\";value:T}interface Box<U> extends Base<U>{tag:string}let b:Box<number>={tag:true,value:8};")]
    public void WrongAssignedValueStaysRejected(string source)=>
        Assert.Contains(Compilation.Check("primitive-union-incorrect-value.ts",source),
            d=>d.Code=="TS2322");

    [Theory]
    [InlineData("interface Base<T>{flag:boolean;value:T}interface Bad<U> extends Base<U>{flag:number}")]
    [InlineData("interface Base<T>{flag:number;value:T}interface Bad<U> extends Base<U>{flag:string}")]
    public void UnequalSharedPropertiesFailClosed(string source)=>
        Assert.Contains(Compilation.Check("primitive-union-incorrect-heritage.ts",source),
            d=>d.Code=="TSCS1001");
}
