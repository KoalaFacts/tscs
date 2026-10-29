using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypeQueryTests
{
    [Theory]
    [InlineData("const item:{value:number}={value:21};type Item=typeof item;let next:Item={value:42};")]
    [InlineData("type T=typeof later;const later:{value:number}={value:21};let x:T={value:42};")]
    [InlineData("const item:{nested:{amount:number}}={nested:{amount:21}};type N=typeof item.nested.amount;let n:N=42;")]
    [InlineData("const item:{label:string;value:number}={label:\"ok\",value:21};let key:keyof typeof item=\"label\";")]
    [InlineData("const item:{value:number}={value:21};let n:(typeof item)[\"value\"]=42;")]
    [InlineData("function twice(n:number):number{return n*2;}type Callback=typeof twice;function apply(fn:Callback):number{return fn(21);}let n:number=apply(twice);")]
    [InlineData("const item:{readonly value:number}={value:21};type T=typeof item;let x:T={value:42};")]
    public void ExplicitValueAndFunctionQueriesCompile(string source)=>
        Assert.Empty(Compilation.Check("type-query.ts",source));

    [Theory]
    [InlineData("type T=typeof missing;", "TS2304")]
    [InlineData("const obj:{value:number}={value:21};type V=typeof obj.absent;", "TS2339")]
    [InlineData("const obj:{value:number}={value:21};type V=typeof obj.value;let bad:V=\"wrong\";", "TS2322")]
    [InlineData("let raw=21;type T=typeof raw;", "TSCS1001")]
    [InlineData("type T=typeof raw;let raw:T=21;", "TS2456")]
    [InlineData("type T=typeof 21;", "TSCS1001")]
    public void InvalidOrUnimplementedQueriesFailClosed(string source,string code)=>
        Assert.Contains(Compilation.Check("type-query-bad.ts",source),d=>d.Code==code);
}
