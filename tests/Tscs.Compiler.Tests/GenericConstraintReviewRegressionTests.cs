using Tscs;
using Xunit;

namespace Tscs.Tests;

// Proposed reviewer regression packet. Product execution is NOT_RUN.
// Reference expectations were executed with pinned TS7-Go 8d9b6da.
public sealed class GenericConstraintReviewRegressionTests
{
    [Theory]
    [InlineData("invalid-dependent-default-unused","type Pair<T extends string,U extends T=string>={first:T;second:U};","TS2344")]
    [InlineData("invalid-dependent-default-explicit","type Pair<T extends string,U extends T=string>={first:T;second:U};let p:Pair<string,string>={first:\"a\",second:\"b\"};","TS2344")]
    [InlineData("invalid-dependent-interface","interface Pair<T extends string,U extends T=string>{first:T;second:U}","TS2344")]
    [InlineData("invalid-unconstrained-dependent-default","type Pair<T,U extends T=number>={first:T;second:U};","TS2344")]
    [InlineData("invalid-structural-dependent-default","interface Shape{value:number}type Pair<T extends Shape,U extends T=Shape>={first:T;second:U};","TS2344")]
    [InlineData("valid-dependent-default","type Pair<T extends string,U extends T=T>={first:T;second:U};let p:Pair<\"hi\">={first:\"hi\",second:\"hi\"};","")]
    [InlineData("valid-constrained-nested-alias","type Box<T extends string>={value:T};type Wrap<U extends string>=Box<U>;let p:Wrap<\"a\">={value:\"a\"};","")]
    [InlineData("valid-constrained-interface","interface Box<T extends string>{value:T}interface Wrap<U extends string>{box:Box<U>}let p:Wrap<\"a\">={box:{value:\"a\"}};","")]
    [InlineData("valid-constrained-inheritance","interface Base<T extends number>{value:T}interface Child<T extends number> extends Base<T>{}let p:Child<1>={value:1};","")]
    [InlineData("valid-dependent-nested","type Pair<T extends string,U extends T>={first:T;second:U};type Wrap<X extends string>=Pair<X,X>;let p:Wrap<\"a\">={first:\"a\",second:\"a\"};","")]
    [InlineData("concrete-invalid-argument","type Box<T extends string>={value:T};let x:Box<number>={value:1};","TS2344")]
    [InlineData("concrete-default-invalid","type Box<T extends number=string>={value:T};","TS2344")]
    [InlineData("independent-opaque-parameters","type Pair<T extends string,U extends T>={first:T;second:U};type Wrap<A extends string,B extends string>=Pair<A,B>;","TS2344")]
    [InlineData("independent-opaque-parameters-used","type Pair<T extends string,U extends T>={first:T;second:U};type Wrap<A extends string,B extends string>=Pair<A,B>;let p:Wrap<\"a\",\"b\">={first:\"a\",second:\"b\"};","TS2344")]
    [InlineData("nested-two-hop-valid","type Box<T extends string>={value:T};type Mid<U extends string>=Box<U>;type Outer<V extends string>=Mid<V>;let p:Outer<\"a\">={value:\"a\"};","")]
    [InlineData("nested-three-hop-valid","type Box<T extends number>={value:T};type Mid<U extends number>=Box<U>;type Outer<V extends number>=Mid<V>;type Last<W extends number>=Outer<W>;let p:Last<1>={value:1};","")]
    [InlineData("related-opaque-parameters-valid","type Pair<T extends string,U extends T>={first:T;second:U};type Wrap<A extends string,B extends A>=Pair<A,B>;","")]
    public void OpaqueParameterRelationsMatchPinnedReference(string id,string source,string expected)
    {
        string[] expectedCodes=expected.Length==0?[]:expected.Split(',');
        var actual=Compilation.Check(id+".ts",source).Select(d=>d.Code).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedCodes,actual);
    }
}
