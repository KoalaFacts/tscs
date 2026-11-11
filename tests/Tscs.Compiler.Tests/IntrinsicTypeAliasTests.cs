using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: the pinned lib.es5.d.ts declares Uppercase, Lowercase, Capitalize,
// Uncapitalize and NoInfer (and es2015.iterable BuiltinIteratorReturn) with
// an `intrinsic` body. The declarations check (pinned checkTypeAliasDeclaration);
// any other alias with that body is TS2795; using one fails closed.
public sealed class IntrinsicTypeAliasTests
{
    [Theory]
    [InlineData("type Uppercase<S extends string> = intrinsic;")]
    [InlineData("type Lowercase<S extends string> = intrinsic;")]
    [InlineData("type Capitalize<S extends string> = intrinsic;")]
    [InlineData("type Uncapitalize<S extends string> = intrinsic;")]
    [InlineData("type NoInfer<T> = intrinsic;")]
    [InlineData("type BuiltinIteratorReturn = intrinsic;")]
    [InlineData("type Uppercase<S extends string> = intrinsic;\ntype NoInfer<T> = intrinsic;\nlet n: number = 1;")]
    public void CompilerProvidedDeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("intrinsic.ts",source));

    [Theory]
    [InlineData("type Foo<T> = intrinsic;")]
    [InlineData("type Foo = intrinsic;")]
    [InlineData("type Uppercase = intrinsic;")]
    [InlineData("type NoInfer<T, U> = intrinsic;")]
    [InlineData("type BuiltinIteratorReturn<T> = intrinsic;")]
    // `intrinsic` as the whole alias body is the keyword even when a type of
    // that name exists (pinned parser: next token is not '.').
    [InlineData("type intrinsic = string;\ntype A = intrinsic;")]
    public void OtherIntrinsicAliasesReportTs2795(string source)
    {
        var diagnostic=Assert.Single(Compilation.Check("intrinsic-error.ts",source));
        Assert.Equal("TS2795",diagnostic.Code);
        Assert.Equal("The 'intrinsic' keyword can only be used to declare compiler provided intrinsic types.",diagnostic.Message);
    }

    [Fact]
    public void ConstraintsOfIntrinsicAliasesAreChecked()=>
        Assert.Contains(Compilation.Check("intrinsic-constraint.ts",
            "type Uppercase<S extends string> = intrinsic;\ntype B = Uppercase<1>;"),d=>d.Code=="TS2344");

    [Theory]
    [InlineData("type Uppercase<S extends string> = intrinsic;\nlet x: Uppercase<\"a\"> = \"A\";")]
    [InlineData("type NoInfer<T> = intrinsic;\nlet x: NoInfer<number> = 1;")]
    [InlineData("type BuiltinIteratorReturn = intrinsic;\nlet x: BuiltinIteratorReturn = 1;")]
    public void UsesFailClosed(string source)=>
        Assert.Contains(Compilation.Check("intrinsic-use.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void IntrinsicStaysAnOrdinaryTypeNameElsewhere()=>
        Assert.Empty(Compilation.Check("intrinsic-name.ts",
            "type intrinsic = string;\ntype A = { value: intrinsic };\nlet a: intrinsic = \"x\";\nlet b: A = { value: a };"));
}
