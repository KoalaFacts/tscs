using Xunit;
using Tscs;
namespace Tscs.Tests;

public class DeclarationBinderTests
{
    private static AmbientSymbolTable Bind(params string[] files)=>
        DeclarationBinder.Bind(files.Select((text,i)=>
        {
            var parsed=DeclarationParser.Parse($"lib.{i}.d.ts",text);
            Assert.Empty(parsed.Diagnostics);
            return parsed;
        }).ToList());

    [Fact]
    public void MergesInterfacesValuesAndNamespacesAcrossFiles()
    {
        var table=Bind(
            "interface Array<T> { length: number; } declare var Array: ArrayConstructor; interface ArrayConstructor {}",
            "interface Array<T> { at(i: number): T; } declare namespace Intl { interface Collator {} var Collator: {}; }",
            "declare namespace Intl { interface Locale {} } declare function parseInt(s: string): number; declare function parseInt(s: string, r: number): number;");
        Assert.Empty(table.Diagnostics);
        var array=table.Globals["Array"];
        Assert.Equal(AmbientMeaning.Interface|AmbientMeaning.FunctionScopedVariable,array.Meaning);
        Assert.Equal(3,array.Declarations.Count);
        var intl=table.Globals["Intl"];
        Assert.Equal(AmbientMeaning.ValueModule|AmbientMeaning.NamespaceModule,intl.Meaning);
        Assert.Equal(["Collator","Locale"],intl.Exports.Keys.Order());
        Assert.Equal(AmbientMeaning.Interface|AmbientMeaning.FunctionScopedVariable,intl.Exports["Collator"].Meaning);
        Assert.Equal(2,table.Globals["parseInt"].Declarations.Count);
    }

    [Fact]
    public void BindsDottedNamespacesAndDeclareGlobalIntoTheirScopes()
    {
        var table=Bind("declare namespace A.B { interface C {} } declare global { interface G {} }");
        Assert.Empty(table.Diagnostics);
        Assert.Equal(AmbientMeaning.NamespaceModule,table.Globals["A"].Meaning);
        Assert.Contains("C",table.Globals["A"].Exports["B"].Exports.Keys);
        Assert.Equal(AmbientMeaning.Interface,table.Globals["G"].Meaning);
    }

    [Fact]
    public void MergesClassesWithInterfacesFunctionsAndNamespaces()
    {
        var table=Bind("declare class K {} interface K {} declare namespace K { var v: number; } declare function K(): void;");
        Assert.Empty(table.Diagnostics);
        Assert.Equal(AmbientMeaning.Class|AmbientMeaning.Interface|AmbientMeaning.ValueModule|AmbientMeaning.Function,
            table.Globals["K"].Meaning);
    }

    [Theory]
    [InlineData("type T = number; type T = string;","TS2300","Duplicate identifier 'T'.")]
    [InlineData("interface T {} type T = string;","TS2300","Duplicate identifier 'T'.")]
    [InlineData("declare class T {} declare class T {}","TS2300","Duplicate identifier 'T'.")]
    [InlineData("declare var T: number; declare function T(): void;","TS2300","Duplicate identifier 'T'.")]
    [InlineData("declare namespace T { var v: number; } declare var T: number;","TS2300","Duplicate identifier 'T'.")]
    [InlineData("declare const T: number; declare var T: number;","TS2451","Cannot redeclare block-scoped variable 'T'.")]
    [InlineData("declare let T: number; declare let T: number;","TS2451","Cannot redeclare block-scoped variable 'T'.")]
    public void ReportsDeclarationsWhoseMeaningsExcludeEachOther(string text,string code,string message)
    {
        var diagnostic=Assert.Single(Bind(text).Diagnostics);
        Assert.Equal(code,diagnostic.Code);
        Assert.Equal(message,diagnostic.Message);
        Assert.Equal(text.LastIndexOf('T'),diagnostic.Start); // anchored on the later declaration
        Assert.Equal(1,diagnostic.Length);
    }

    [Fact]
    public void AllowsTypeOnlyNamespaceToShareAVariableName()
    {
        Assert.Empty(Bind("declare namespace T { interface I {} } declare var T: number;").Diagnostics);
    }

    [Fact]
    public void RefusesFilesWithSyntaxErrors()
    {
        var broken=DeclarationParser.Parse("lib.bad.d.ts","interface {");
        Assert.NotEmpty(broken.Diagnostics);
        Assert.Throws<ArgumentException>(()=>DeclarationBinder.Bind([broken]));
    }
}
