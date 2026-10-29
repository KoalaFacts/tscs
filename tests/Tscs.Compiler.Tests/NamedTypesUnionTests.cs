using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class NamedTypesUnionTests
{
    [Theory]
    [InlineData("type Value = number; let n: Value = 21;", "plain alias")]
    [InlineData("interface Item { value: number; label: string } let x: Item = { value: 21, label: \"ok\" };", "interface")]
    [InlineData("type Item = { value: number }; interface Container { item: Item } let x: Container = { item: {value: 1} };", "nested alias")]
    [InlineData("type Item = { value: number }; type Extended = { value: number; name: string }; let a: Extended = {value: 1,name: \"n\"}; let b: Item = a;", "width assignment")]
    [InlineData("type Choice = number | string; let a: Choice = 1; a = \"label\";", "union write")]
    [InlineData("type Choice = number | string; let a: Choice = \"label\"; let b: Choice = 12;", "union alternatives")]
    [InlineData("type Choice = {value: number} | {value: string}; let a: Choice = {value: 1}; a = {value: \"x\"};", "object union")]
    [InlineData("type Choice = {value: number, a: boolean} | {value: number, b: boolean}; function get(c: Choice): number {return c.value;} let x: number = get({value: 1, a: true});", "union common member")]
    [InlineData("type List = (number | string)[]; let a: List = [1, \"two\"]; let x: number | string = a[0];", "array union")]
    [InlineData("type Value = number | string; function echo(x: Value): Value { return x; } let a: Value = echo(1); let b: Value = echo(\"x\");", "union function")]
    [InlineData("type A = number; type B = A; type C = B; let n: C = 2;", "forward names")]
    public void NamedAndUnionTypesTraverseTheActualChecker(string source,string _) =>
        Assert.Empty(Compilation.Check("types.ts",source));

    [Theory]
    [InlineData("type T = number; let x: T = \"bad\";", "TS2322")]
    [InlineData("interface T { value: number } let x: T = { value: \"bad\" };", "TS2322")]
    [InlineData("type Choice = number | string; let x: Choice = false;", "TS2322")]
    [InlineData("type Choice = number | string; function f(x: Choice): number { return x; }", "TS2322")]
    [InlineData("type Loop = Loop; let x: Loop;", "TS2456")]
    [InlineData("type A = B; type B = A;", "TS2456")]
    [InlineData("type A = Missing; let x: A;", "TS2304")]
    [InlineData("type A = number; type A = string;", "TS2300")]
    [InlineData("type A = {value: number} | {other: number}; let a: A = {value: 1}; a.other;", "TS2339")]
    [InlineData("type A = {value: number} | {value: string}; let a: A = {value: 1}; a.value = 3;", "TSCS1001")]
    [InlineData("interface T extends Missing { value: number }", "TS2304")]
    [InlineData("type T<TItem> = TItem;", "TSCS1001")]
    [InlineData("type A = number |;", "TSCS1001")]
    [InlineData("type A = {value: number} | {other: number}; let a: A = {value: 1, wrong: true};", "TS2353")]
    public void MissingOrIncorrectSemanticsNeverTurnIntoSuccess(string source,string code) =>
        Assert.Contains(Compilation.Check("bad.ts",source), d=>d.Code==code);

    [Fact]
    public void SeparateTypeAndValueNamespacesHaveIndependentIdentity()
    {
        const string source="type Result = number; let Result: Result = 42; let answer: number = Result;";
        Assert.Empty(Compilation.Check("types.ts",source));
    }

    [Fact]
    public void TypeOnlyImportsAndExportedFunctionsWorkAcrossActualModuleGraph()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-named-union");
        var files=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"models.ts")]=
                "export interface Reading { value: number; label: string } export type Choice = Reading | number;",
            [Path.Combine(root,"logic.ts")]=
                "import type { Reading, Choice } from \"./models.js\"; export function twice(input: Reading): Reading {return {value: input.value * 2,label: input.label};} export function passthrough(x: Choice): Choice {return x;}",
            [Path.Combine(root,"entry.ts")]=
                "import type { Reading, Choice } from \"./models.js\"; import { twice,passthrough } from \"./logic.js\"; const seed: Reading = {value: 21,label: \"seed\"}; let data: Choice = seed; data = 1; export const result: number = twice(seed).value; export const label: string = twice(seed).label;"
        };
        var program=CompilerProgram.Load(new TestHost(files),[Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Empty(program.Analyze().Diagnostics);
        var emitted=program.EmitJavaScript();
        Assert.Equal(3,emitted.Count);
        Assert.All(emitted,output=>Assert.DoesNotContain("import type",output.Text,StringComparison.Ordinal));
        Assert.DoesNotContain(emitted,output=>output.Text.Contains("interface Reading",StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidCrossModuleTypeImportDoesNotSilentlyBecomeAny()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-named-types-negative");
        var files=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"models.ts")]="export type A = number;",
            [Path.Combine(root,"entry.ts")]="import type { Missing } from \"./models.js\"; let x: Missing = 2;"
        };
        var program=CompilerProgram.Load(new TestHost(files),[Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2305");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    private sealed class TestHost(IReadOnlyDictionary<string,string> sources):ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>sources[path];
    }
}
