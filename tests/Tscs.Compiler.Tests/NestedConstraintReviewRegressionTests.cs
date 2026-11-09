using Tscs;
using Xunit;

namespace Tscs.Tests;

// Pinned TS7-Go 8d9b6da: bad-* report TS2344, good-* exit 0. Without a
// relational proof for a type parameter nested in an object, alias argument
// or union constraint, tscs fails closed (TSCS1001) on all of these rather
// than accepting the invalid ones; any/never defaults remain proven.
public sealed class NestedConstraintReviewRegressionTests
{
    [Theory]
    [InlineData("bad-structural-default","type Pair<T extends string,U extends {value:T}={value:string}>={first:T;second:U};","TSCS1001")]
    [InlineData("bad-structural-default-used","type Pair<T extends string,U extends {value:T}={value:string}>={first:T;second:U};let x:Pair<\"a\",{value:\"a\"}>={first:\"a\",second:{value:\"a\"}};","TSCS1001")]
    [InlineData("good-structural-default","type Pair<T extends string,U extends {value:T}={value:T}>={first:T;second:U};","TSCS1001")]
    [InlineData("bad-structural-independent","type Box<T,U extends {value:T}>={first:T;second:U};type Wrap<A extends string,B extends string>=Box<A,{value:B}>;","TSCS1001")]
    [InlineData("good-structural-related","type Box<T,U extends {value:T}>={first:T;second:U};type Wrap<A extends string,B extends A>=Box<A,{value:B}>;","TSCS1001")]
    [InlineData("bad-alias-default","type Id<X>=X;type Pair<T extends string,U extends Id<T>=string>={first:T;second:U};","TSCS1001")]
    [InlineData("good-alias-default","type Id<X>=X;type Pair<T extends string,U extends Id<T>=T>={first:T;second:U};","TSCS1001")]
    [InlineData("bad-union-default","type Pair<T extends string,U extends T|never=string>={first:T;second:U};","TSCS1001")]
    [InlineData("good-union-default","type Pair<T extends string,U extends T|never=T>={first:T;second:U};","TSCS1001")]
    [InlineData("good-never-default","type Pair<T extends string,U extends T=never>={first:T;second:U};","")]
    [InlineData("good-any-default","type Pair<T extends string,U extends T=any>={first:T;second:U};","")]
    public void NestedParameterRelationsMatchPinnedReference(string id,string source,string expected)
    {
        string[] expectedCodes=expected.Length==0?[]:expected.Split(',');
        var actual=Compilation.Check(id+".ts",source).Select(d=>d.Code).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedCodes,actual);
    }
}
