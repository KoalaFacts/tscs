using Tscs;
using Xunit;

namespace Tscs.Tests;

// Pinned TS7-Go 8d9b6da: bad-* report TS2344, good-* exit 0. Without a
// finite structural, alias and T|never relations can be proved without
// erasing opaque identities. Invalid independent/supertype cases still fail
// closed (TSCS1001); any/never defaults retain their existing rules.
public sealed class NestedConstraintReviewRegressionTests
{
    [Theory]
    [InlineData("bad-structural-default","type Pair<T extends string,U extends {value:T}={value:string}>={first:T;second:U};","TSCS1001")]
    [InlineData("bad-structural-default-used","type Pair<T extends string,U extends {value:T}={value:string}>={first:T;second:U};let x:Pair<\"a\",{value:\"a\"}>={first:\"a\",second:{value:\"a\"}};","TSCS1001")]
    [InlineData("good-structural-default","type Pair<T extends string,U extends {value:T}={value:T}>={first:T;second:U};","")]
    [InlineData("bad-structural-independent","type Box<T,U extends {value:T}>={first:T;second:U};type Wrap<A extends string,B extends string>=Box<A,{value:B}>;","TSCS1001")]
    [InlineData("good-structural-related","type Box<T,U extends {value:T}>={first:T;second:U};type Wrap<A extends string,B extends A>=Box<A,{value:B}>;","")]
    [InlineData("bad-alias-default","type Id<X>=X;type Pair<T extends string,U extends Id<T>=string>={first:T;second:U};","TSCS1001")]
    [InlineData("good-alias-default","type Id<X>=X;type Pair<T extends string,U extends Id<T>=T>={first:T;second:U};","")]
    [InlineData("bad-union-default","type Pair<T extends string,U extends T|never=string>={first:T;second:U};","TSCS1001")]
    [InlineData("good-union-default","type Pair<T extends string,U extends T|never=T>={first:T;second:U};","")]
    [InlineData("good-never-default","type Pair<T extends string,U extends T=never>={first:T;second:U};","")]
    [InlineData("bad-any-against-never-default","type None<X>=never;type Bad<T,U extends None<T>=any>={value:U};","TS2344")]
    [InlineData("bad-any-against-never-forwarded","type None<X>=never;type Box<T,U extends None<T>>={value:U};type Wrap<X>=Box<X,any>;","TS2344")]
    [InlineData("good-never-against-never-default","type None<X>=never;type Bad<T,U extends None<T>=never>={value:U};","")]
    [InlineData("good-never-against-never-forwarded","type None<X>=never;type Box<T,U extends None<T>>={value:U};type Wrap<X>=Box<X,never>;","")]
    [InlineData("good-any-structural-default","type Pair<T extends string,U extends {value:T}=any>={first:T;second:U};","")]
    [InlineData("good-any-default","type Pair<T extends string,U extends T=any>={first:T;second:U};","")]
    public void NestedParameterRelationsMatchPinnedReference(string id,string source,string expected)
    {
        string[] expectedCodes=expected.Length==0?[]:expected.Split(',');
        var actual=Compilation.Check(id+".ts",source).Select(d=>d.Code).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedCodes,actual);
    }
}
