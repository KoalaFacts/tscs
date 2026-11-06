using Xunit;
using Tscs;
namespace Tscs.Tests;

public class DeclarationBinderTests
{
    private static AmbientDeclarationFile Parse(string file,string text)
    {
        var result=DeclarationParser.Parse(file,text);
        Assert.Empty(result.Diagnostics);
        return result;
    }
    private static AmbientGlobals Bind(params string[] texts)=>
        DeclarationBinder.Bind(texts.Select((t,i)=>Parse($"lib.{i}.d.ts",t)));

    [Fact]
    public void MergesInterfacesAndVariablesAcrossFilesLikeTheStandardLibrary()
    {
        var globals=Bind(
            "interface Array<T> { length: number; } interface ArrayConstructor { isArray(arg: any): arg is any[]; } declare var Array: ArrayConstructor;",
            "interface Array<T> { find(predicate: (value: T) => unknown): T | undefined; }");
        Assert.Empty(globals.Diagnostics);
        var array=globals.Symbols["Array"];
        Assert.Equal(AmbientMeaning.Interface|AmbientMeaning.FunctionScopedVariable,array.Meanings);
        Assert.Equal(3,array.Declarations.Count);
        Assert.Equal(2,array.Declarations.OfType<AmbientInterface>().SelectMany(i=>i.Members).Count());
        Assert.Equal(["Array","ArrayConstructor"],globals.Symbols.Keys.Order());
    }

    [Fact]
    public void MergesNamespacesFunctionOverloadsClassesAndGlobalAugmentations()
    {
        var globals=Bind(
            "declare namespace Intl { interface Collator { compare(x: string, y: string): number; } var Collator: { new (): Collator }; }"
            +" declare function parseInt(s: string): number; declare function parseInt(s: string, radix: number): number;"
            +" declare class Box { value: number; } interface Box { label: string; }",
            "declare namespace Intl { interface NumberFormat { format(n: number): string; } }"
            +" declare namespace A.B { type Only = string; }"
            +" declare global { interface Window { name: string; } }");
        Assert.Empty(globals.Diagnostics);
        var intl=globals.Symbols["Intl"];
        Assert.Equal(AmbientMeaning.ValueModule|AmbientMeaning.NamespaceModule,intl.Meanings);
        Assert.Equal(["Collator","NumberFormat"],intl.Exports.Keys.Order());
        Assert.Equal(AmbientMeaning.Interface|AmbientMeaning.FunctionScopedVariable,intl.Exports["Collator"].Meanings);
        Assert.Equal(2,globals.Symbols["parseInt"].Declarations.Count);
        Assert.Equal(AmbientMeaning.Class|AmbientMeaning.Interface,globals.Symbols["Box"].Meanings);
        var a=globals.Symbols["A"];
        Assert.Equal(AmbientMeaning.NamespaceModule,a.Meanings);
        Assert.Equal(AmbientMeaning.NamespaceModule,a.Exports["B"].Meanings);
        Assert.Equal(AmbientMeaning.TypeAlias,a.Exports["B"].Exports["Only"].Meanings);
        Assert.Equal(AmbientMeaning.Interface,globals.Symbols["Window"].Meanings);
    }

    [Theory]
    [InlineData("type T = string; interface T { }","TS2300","Duplicate identifier 'T'.")]
    [InlineData("interface T { } type T = string;","TS2300","Duplicate identifier 'T'.")]
    [InlineData("declare var x: number; declare class x { }","TS2300","Duplicate identifier 'x'.")]
    [InlineData("declare var x: number; declare let x: number;","TS2451","Cannot redeclare block-scoped variable 'x'.")]
    [InlineData("declare const x: number; declare var x: number;","TS2451","Cannot redeclare block-scoped variable 'x'.")]
    [InlineData("declare function f(): void; declare var f: number;","TS2300","Duplicate identifier 'f'.")]
    [InlineData("declare namespace N { var v: number; } declare var N: number;","TS2300","Duplicate identifier 'N'.")]
    public void ReportsDeclarationsTheEarlierMeaningsExclude(string text,string code,string message)
    {
        var globals=Bind(text);
        var diagnostic=Assert.Single(globals.Diagnostics);
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(message,diagnostic.Message);
        Assert.Equal("lib.0.d.ts",diagnostic.File);
        // The excluded declaration is not merged into the symbol.
        Assert.Single(globals.Symbols.Values.Single().Declarations);
    }

    [Fact]
    public void AllowsTheMergesTheReferenceAllows()
    {
        var globals=Bind("declare var v: number; declare var v: number;"
            +" declare namespace N { interface I { } } declare var N: number;"
            +" declare function g(): void; declare namespace g { var x: number; }"
            +" declare class C { } declare namespace C { var y: number; }");
        Assert.Empty(globals.Diagnostics);
        Assert.Equal(2,globals.Symbols["v"].Declarations.Count);
        Assert.Equal(AmbientMeaning.NamespaceModule|AmbientMeaning.FunctionScopedVariable,globals.Symbols["N"].Meanings);
        Assert.Equal(AmbientMeaning.Function|AmbientMeaning.ValueModule,globals.Symbols["g"].Meanings);
        Assert.Equal(AmbientMeaning.Class|AmbientMeaning.ValueModule,globals.Symbols["C"].Meanings);
    }
}
