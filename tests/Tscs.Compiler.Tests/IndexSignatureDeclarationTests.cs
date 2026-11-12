using Tscs;
using Xunit;

namespace Tscs.Tests;

// A2.3: the pinned lib.es5.d.ts declares ImportAssertions, ImportAttributes,
// ConcatArray<T> and ArrayLike<T> with index signatures. The declarations
// check (pinned checkGrammarIndexSignature); using an indexed type fails
// closed until index signatures are modelled.
public sealed class IndexSignatureDeclarationTests
{
    [Theory]
    [InlineData("interface ImportAttributes {\n    [key: string]: string;\n}")]
    [InlineData("interface ArrayLike<T> {\n    readonly length: number;\n    readonly [n: number]: T;\n}")]
    [InlineData("interface ConcatArray<T> {\n    readonly length: number;\n    readonly [n: number]: T;\n    join(separator?: string): string;\n    slice(start?: number, end?: number): T[];\n}")]
    [InlineData("type A = { [k: string]: number, b: number };")]
    [InlineData("type K = string | number;\ninterface A { [k: K]: boolean }")]
    [InlineData("interface A { readonly: string; [readonly: number]: string }")]
    public void DeclarationsCheck(string source)=>
        Assert.Empty(Compilation.Check("index.ts",source));

    [Theory]
    [InlineData("interface A { [k: boolean]: string }","TS1268",15,
        "An index signature parameter type must be 'string', 'number', 'symbol', or a template literal type.")]
    [InlineData("interface A { [k: any]: string }","TS1268",15,
        "An index signature parameter type must be 'string', 'number', 'symbol', or a template literal type.")]
    [InlineData("interface A { [k: \"a\"]: string }","TS1337",15,
        "An index signature parameter type cannot be a literal type or generic type. Consider using a mapped object type instead.")]
    [InlineData("type A = { [k: 1 | 2]: string };","TS1337",12,
        "An index signature parameter type cannot be a literal type or generic type. Consider using a mapped object type instead.")]
    [InlineData("interface A { [k: string]: Missing }","TS2304",27,"Cannot find name 'Missing'.")]
    [InlineData("interface G<T> { [n: number]: Missing }","TS2304",30,"Cannot find name 'Missing'.")]
    [InlineData("interface G<T> { [k: boolean]: T }","TS1268",18,
        "An index signature parameter type must be 'string', 'number', 'symbol', or a template literal type.")]
    public void InvalidDeclarationsMatchThePinnedChecker(string source,string code,int start,string message)
    {
        var diagnostic=Assert.Single(Compilation.Check("index-error.ts",source));
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(start,diagnostic.Start);
        Assert.Equal(message,diagnostic.Message);
    }

    [Theory]
    // Uses, generic keys, heritage, reopening and declaration identity are not modelled.
    [InlineData("interface A { [k: string]: string }\nlet a: A = {};")]
    [InlineData("type A = { [k: string]: string };\ntype B = A[\"x\"];")]
    [InlineData("interface A { [k: string]: string }\ninterface B extends A { b: string }")]
    [InlineData("interface B { b: string }\ninterface A extends B { [k: string]: string }")]
    [InlineData("interface A { [k: string]: string }\ninterface A { [n: number]: string }")]
    [InlineData("interface A { [k: string]: string; [n: number]: string }")]
    [InlineData("interface G<K extends string> { [k: K]: number }")]
    [InlineData("let a: { [k: string]: number } = {};")]
    public void UnmodelledFormsFailClosed(string source)=>
        Assert.Contains(Compilation.Check("index-use.ts",source),d=>d.Code=="TSCS1001");
}
