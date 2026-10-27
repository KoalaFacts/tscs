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

    [Theory]
    [InlineData("function f(): number { return\n1; }")]
    [InlineData("function f(): number { return\r\n1; }")]
    [InlineData("function f(): number { return/* newline\ninside */1; }")]
    [InlineData("function f(): number { return/* newline\r\ninside */1; }")]
    [InlineData("function f(): number { return // comment\n1; }")]
    public void ReturnCannotConsumeAnExpressionAcrossALineTerminator(string source)
    {
        var diagnostics=Compilation.Check("source.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001" &&
            d.Start==source.IndexOf("return",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("function f(): number { return 1; } let n: number = f();")]
    [InlineData("function f(): number { return /* same line */ 1; } let n: number = f();")]
    [InlineData("function f(x: number): number { { return x; } } let n: number = f(3);")]
    [InlineData("function f(x: number): number { let y: number = x; { let z: number = y; return z; } } let n: number = f(3);")]
    public void StraightLineFunctionReturnFlowIncludesNestedUnconditionalBlocks(string source) =>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Fact]
    public void NestedBlocksWithoutAValueReturnStillFailClosed()
    {
        var diagnostics=Compilation.Check("source.ts","function f(x: number): number { { let y: number = x; } }");
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
    }

    [Theory]
    [InlineData("function choose(flag: boolean): number { if (flag) { return 1; } else { return 2; } } let n: number = choose(true);")]
    [InlineData("function choose(flag: boolean): number { if (flag) { return 1; } return 2; } let n: number = choose(false);")]
    [InlineData("function choose(a: boolean, b: boolean): number { if (a) { return 1; } else if (b) { return 2; } else { return 3; } } let n: number = choose(false, true);")]
    [InlineData("function choose(flag: boolean): number { let n: number; if (flag) { n = 1; } else { n = 2; } return n; }")]
    [InlineData("function choose(flag: any): number { if (flag) { return 1; } else { return 2; } }")]
    public void BracedConditionalReturnPathsAndDefiniteAssignmentWork(string source) =>
        Assert.Empty(Compilation.Check("source.ts",source));

    [Theory]
    [InlineData("function missing(flag: boolean): number { if (flag) { return 1; } }","TSCS1001")]
    [InlineData("function missing(flag: boolean): number { if (flag) { return 1; } else { let x = 1; } }","TSCS1001")]
    [InlineData("function missing(flag: boolean): number { let n: number; if (flag) { n = 1; } return n; }","TSCS1001")]
    [InlineData("function bad(flag: boolean): number { if (flag) { return \"bad\"; } else { return 1; } }","TS2322")]
    [InlineData("function bad(flag: boolean): number { if (flag) { let inner = 1; } return inner; }","TS2304")]
    [InlineData("function unsupported(flag: number): number { if (flag) { return 1; } else { return 2; } }","TSCS1001")]
    [InlineData("function unsupported(flag: boolean): number { if (flag) return 1; else { return 2; } }","TSCS1001")]
    public void ConditionalBoundariesRejectIncompleteOrUnsupportedPrograms(string source,string code) =>
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
