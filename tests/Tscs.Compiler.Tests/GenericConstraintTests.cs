using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: checked constraints in both declarations and type instantiations.
public sealed class GenericConstraintTests
{
    [Theory]
    [InlineData("type Box<T extends number>={value:T};let box:Box<1>={value:1};let n:number=box.value;")]
    [InlineData("type Box<T extends string=string>={value:T};let box:Box={value:\"a\"};let s:string=box.value;")]
    [InlineData("interface Shape{value:number}interface Holder<T extends Shape>{item:T}let h:Holder<Shape>={item:{value:8}};")]
    [InlineData("type Pair<T extends string,U extends T=T>={first:T;second:U};let p:Pair<\"hi\">={first:\"hi\",second:\"hi\"};")]
    [InlineData("interface Base<T extends number=number>{value:T}interface Child extends Base{other:number}let c:Child={value:8,other:2};")]
    public void ValidConstrainedTypesCheck(string source)=>
        Assert.Empty(Compilation.Check("constraint.ts",source));

    [Theory]
    [InlineData("type Box<T extends number>={value:T};let b:Box<string>={value:\"no\"};","TS2344")]
    [InlineData("type Box<T extends number=string>={value:T};","TS2344")]
    [InlineData("interface Shape{value:number}type Box<T extends Shape>={value:T};let b:Box<string>={value:\"no\"};","TS2344")]
    [InlineData("type Box<T extends Missing>={value:T};","TS2304")]
    public void InvalidConstrainedTypesReportErrors(string source,string expected)=>
        Assert.Contains(Compilation.Check("constraint-error.ts",source),d=>d.Code==expected);

    [Theory]
    [InlineData("type Outer=number;type Box<T extends T>={value:T};")]
    [InlineData("interface Box<T extends Later,Later=number>{value:T}")]
    [InlineData("interface Box<T extends string>{value:T}interface Box<T>{other:T}")]
    public void UnprovedConstraintsFailClosed(string source)=>
        Assert.NotEmpty(Compilation.Check("constraint-unsupported.ts",source));
}
