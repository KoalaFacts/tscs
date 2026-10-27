using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class FunctionPipelineTests
{
    [Theory]
    [InlineData("function twice(x: number): number { return x + x; } let result: number = twice(21);")]
    [InlineData("function keep(flag: boolean, value: string): string { return value; } const result: string = keep(true, \"ok\");")]
    [InlineData("let result: number = twice(21); function twice(x: number): number { return x + x; }")]
    [InlineData("function twice(x: number): number { let y: number = x + x; return y; } let result = twice(21);")]
    public void ExplicitlyTypedFunctionsBindAndCheckThroughTheNormalPipeline(string source) =>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("function id(x: number): number { return x; } let n = id(\"wrong\");", "TS2345")]
    [InlineData("function id(x: number): number { return x; } let n = id();", "TS2554")]
    [InlineData("function bad(x: number): number { return \"wrong\"; }", "TS2322")]
    [InlineData("let x: number = 1; x(1);", "TSCS1001")]
    [InlineData("function broken(x): number { return x; }", "TSCS1001")]
    [InlineData("function missing(x: number): number { let y = x; }", "TSCS1001")]
    [InlineData("return 1;", "TSCS1001")]
    public void FunctionsFailClosedOnUnsupportedOrIncorrectSemantics(string source,string code) =>
        Assert.Contains(Compilation.Check("source.ts",source),d=>d.Code==code);

    [Fact]
    public void CallsCanFollowAHostResolvedNamedExport()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-function-links");
        var main=Path.GetFullPath(Path.Combine(root,"main.ts"));
        var math=Path.GetFullPath(Path.Combine(root,"math.ts"));
        var host=new MemoryHost(new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [main]="import { twice } from \"./math\"; let result: number = twice(4);",
            [math]="export function twice(x: number): number { return x + x; }"
        });
        Assert.Empty(CompilerProgram.Load(host,[main]).Analyze().Diagnostics);
    }

    private sealed class MemoryHost(IReadOnlyDictionary<string,string> sources) : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string canonicalFileName) => sources[canonicalFileName];
    }
}
