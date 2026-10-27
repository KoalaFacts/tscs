using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ObjectDataTests
{
    [Theory]
    [InlineData("const data = { value: 21, label: \"ok\" }; let result: number = data.value + data.value; data.value = 42;")]
    [InlineData("let data: { value: number; label: string } = { value: 1, label: \"ok\" }; let s: string = data.label;")]
    [InlineData("const data = { nested: { value: 21 } }; data.nested.value = 42; let n: number = data.nested.value;")]
    [InlineData("function twice(data: { value: number }): { value: number } { return { value: data.value * 2 }; } let n: number = twice({ value: 21 }).value;")]
    [InlineData("const wide = { value: 21, label: \"ok\" }; let narrow: { value: number } = wide;")]
    [InlineData("const wide = { nested: { value: 21, label: \"ok\" } }; let narrow: { nested: { value: number } } = wide;")]
    [InlineData("const data = { \"value\": 21 }; let n: number = data.value; (data).value = 42;")]
    [InlineData("let data: { value: number, nested: { flag: boolean, }, } = { value: 1, nested: { flag: true, }, };")]
    [InlineData("const data = { value: 21 }; let boxed = { data: data }; let n: number = boxed.data.value;")]
    [InlineData("let data: {} = 1; const empty = {}; let other: {} = empty;")]
    [InlineData("let n: number = ({ value: 42 }).value; ({ value: n });")]
    [InlineData("function pass(x: { value: number }): { value: number } { return x; } const wide = { value: 1, extra: true }; let n = pass(wide).value;")]
    public void ObjectProgramsUseTheExistingCompilerPipeline(string source) =>
        Assert.Empty(Compilation.Check("objects.ts",source));

    [Theory]
    [InlineData("const data = { value: 1 }; data.missing;", "TS2339")]
    [InlineData("const data = { value: 1 }; data.missing = 2;", "TS2339")]
    [InlineData("const data = { value: 1 }; data.value = \"bad\";", "TS2322")]
    [InlineData("const data: { value: number } = { value: \"bad\" };", "TS2322")]
    [InlineData("const data: { value: number } = {};", "TS2741")]
    [InlineData("function f(x: { value: number }): number { return x.value; } f({ value: \"bad\" });", "TS2322")]
    [InlineData("function f(): { value: number } { return { value: \"bad\" }; }", "TS2322")]
    [InlineData("const data: { value: number } = { value: 1, extra: true };", "TS2353")]
    [InlineData("const data: { nested: { value: number } } = { nested: { value: 1, extra: true } };", "TS2353")]
    [InlineData("const data = { value: 1, value: 2 };", "TS1117")]
    [InlineData("let data: { value: number }; data.value = 1;", "TSCS1001")]
    [InlineData("const data = { value: 1 }; data = { value: 2 };", "TS2588")]
    [InlineData("let n = 1; n.value;", "TS2339")]
    public void InvalidObjectProgramsRetainRealDiagnostics(string source,string code) =>
        Assert.Contains(Compilation.Check("objects.ts",source),d=>d.Code==code);

    [Theory]
    [InlineData("let x: { value?: number } = {};")]
    [InlineData("let x: { readonly value: number } = { value: 1 };")]
    [InlineData("const x = { get value() { return 1; } };")]
    [InlineData("const x = { value() { return 1; } };")]
    [InlineData("const x = { ...other };")]
    [InlineData("const x = { [name]: 1 };")]
    [InlineData("const x = { __proto__: {} };")]
    public void UnimplementedObjectSemanticsFailExplicitly(string source) =>
        Assert.Contains(Compilation.Check("objects.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void MissingMemberDiagnosticRetainsThePropertyTokenUtf16Location()
    {
        const string source="/* 😀 */ const data = { value: 1 }; data.missing;";
        var diagnostic=Assert.Single(Compilation.Check("objects.ts",source));
        Assert.Equal("TS2339",diagnostic.Code);
        Assert.Equal(source.IndexOf("missing",StringComparison.Ordinal),diagnostic.Start);
        Assert.Equal(7,diagnostic.Length);
    }

    [Fact]
    public void ObjectsCrossModuleFunctionsAndReexportsWithOneSharedProgram()
    {
        var program=LoadGraph("import { seed } from \"./data.js\"; import { twice } from \"./bridge.js\"; seed.value = 21; export const result: number = twice(seed).value;");
        Assert.Empty(program.Analyze().Diagnostics);
        var output=program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        Assert.Contains(output,source=>source.Text.Contains("seed.value = 21;",StringComparison.Ordinal));
        Assert.All(output,source=>Assert.DoesNotContain(": number",source.Text));
    }

    [Fact]
    public void InvalidPropertyWriteInOneModulePreventsOutputOfTheEntireProgram()
    {
        var program=LoadGraph("import { seed } from \"./data.js\"; seed.value = \"wrong\";");
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    [Fact]
    public void ObjectNestingLimitsFailWithoutCrashing()
    {
        var deepType=string.Concat(Enumerable.Repeat("{ value: ",100))+"number"+new string('}',100);
        var deepLiteral=string.Concat(Enumerable.Repeat("{ value: ",100))+"1"+new string('}',100);
        Assert.Contains(Compilation.Check("type.ts","let x: "+deepType+";"),d=>d.Code=="TSCS1001");
        Assert.Contains(Compilation.Check("literal.ts","let x = "+deepLiteral+";"),d=>d.Code=="TSCS1001");
    }

    [Fact]
    public void SharedObjectGraphsHaveBoundedDiagnosticDisplay()
    {
        var source="const n0 = { value: 1 };";
        for(int i=1;i<=12;i++)source+=$" const n{i} = {{ left: n{i-1}, right: n{i-1} }};";
        source+=" n12.missing;";
        var diagnostic=Assert.Single(Compilation.Check("shared.ts",source));
        Assert.Equal("TS2339",diagnostic.Code);
        Assert.True(diagnostic.Message.Length<4300,"Shared type graphs must not expand exponentially into diagnostics.");
    }

    [Theory]
    [InlineData("let x: { a: number } = ({ a: 1, extra: true });", "TS2353")]
    [InlineData("function f(x: { a: number }): number { return x.a; } f({ a: 1, extra: true });", "TS2353")]
    [InlineData("function f(): { a: number } { return { a: 1, extra: true }; }", "TS2353")]
    [InlineData("let x = { nested: { a: 1 } }; x.nested = { a: 1, extra: true };", "TS2353")]
    [InlineData("let x = { nested: { a: 1 } }; x.nested = { a: \"bad\" };", "TS2322")]
    public void FreshnessIsCheckedAtEveryObjectConsumptionSite(string source,string code)=>
        Assert.Contains(Compilation.Check("freshness.ts",source),d=>d.Code==code);

    [Fact]
    public void FreshObjectArgumentUsesPinnedPropertyDiagnostic()
    {
        // Actual pinned Go run 40602154110, object-data:negative-06.
        const string source="function f(x: { value: number }): number { return x.value; } f({ value: \"bad\" });";
        var error=Assert.Single(Compilation.Check("input.ts",source));
        Assert.Equal("TS2322",error.Code);
        Assert.Equal("Type 'string' is not assignable to type 'number'.",error.Message);
        Assert.Equal(65,error.Start);
        Assert.Equal(5,error.Length);
    }

    [Fact]
    public void NonLiteralObjectArgumentRetainsWholeArgumentMismatch()
    {
        const string source="function f(x: { value: number }): number { return x.value; } const data = { value: \"bad\" }; f(data);";
        var error=Assert.Single(Compilation.Check("input.ts",source));
        Assert.Equal("TS2345",error.Code);
    }

    private static CompilerProgram LoadGraph(string entry)
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-object-data");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"entry.ts")]=entry,
            [Path.Combine(root,"data.ts")]="export const seed = { value: 20, label: \"seed\" };",
            [Path.Combine(root,"math.ts")]="export function twice(data: { value: number }): { value: number } { return { value: data.value * 2 }; }",
            [Path.Combine(root,"bridge.ts")]="export { twice } from \"./math.js\";"
        };
        return CompilerProgram.Load(new MemoryHost(sources),[Path.Combine(root,"entry.ts")],
            new CompilerOptions { NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015 });
    }

    private sealed class MemoryHost(IReadOnlyDictionary<string,string> sources) : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string canonicalFileName)=>sources[canonicalFileName];
    }
}
