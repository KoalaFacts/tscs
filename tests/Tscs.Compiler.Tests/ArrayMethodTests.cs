using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler behavior; not original TS7 conformance acceptance.
public sealed class ArrayMethodTests
{
    [Theory]
    [InlineData("let xs: number[] = [21]; let n: number = xs.push(22);")]
    [InlineData("let xs: Array<number> = [21]; let n: number = xs.unshift(20, 22);")]
    [InlineData("let xs: number[] = [21]; let n: number = xs.push();")]
    [InlineData("let xs: number[] = [21]; let n: number | undefined = xs.pop();")]
    [InlineData("let xs: number[] = [21]; let n: number | undefined = xs.shift();")]
    [InlineData("let xs: number[] = []; let n: number | undefined = xs.pop();")]
    [InlineData("let xs: string[] = [\"ok\"]; let s: string | undefined = xs.shift();")]
    [InlineData("let xs: number[] = [1]; let more: number[] = [2, 3]; let n: number = xs.push(...more);")]
    [InlineData("let xs: number[][] = [[21]]; xs.push([42]); let n: number = xs[1][0];")]
    [InlineData("let xs: { value: number }[] = [{value:21}]; xs.push({value:42});")]
    [InlineData("function append(xs: number[], n: number): number { return xs.push(n); } let size: number = append([21], 22);")]
    [InlineData("type Numbers = Array<number>; let xs: Numbers = [21]; let n: number = xs.push(22);")]
    public void MutableArrayMethodsUseTheCheckedCallPipeline(string source) =>
        Assert.Empty(Compilation.Check("array-methods.ts", source));

    [Theory]
    [InlineData("let xs: number[] = [21]; xs.push(\"bad\");", "TS2345")]
    [InlineData("let xs: number[] = [21]; xs.unshift(\"bad\");", "TS2345")]
    [InlineData("let xs: number[] = [21]; xs.pop(1);", "TS2554")]
    [InlineData("let xs: number[] = [21]; xs.shift(1);", "TS2554")]
    [InlineData("let xs: readonly number[] = [21]; xs.push(22);", "TS2339")]
    [InlineData("let xs: ReadonlyArray<number> = [21]; xs.pop();", "TS2339")]
    [InlineData("let xs: readonly number[] = [21]; xs.unshift(20);", "TS2339")]
    [InlineData("let xs: ReadonlyArray<number> = [21]; xs.shift();", "TS2339")]
    [InlineData("let xs: string[] = [\"hi\"]; xs.push(21);", "TS2345")]
    [InlineData("let xs: number[] = [21]; let result: string = xs.push(22);", "TS2322")]
    public void WrongArgumentsAndReadonlyMutatorsFailClosed(string source, string code) =>
        Assert.Contains(Compilation.Check("invalid-array-methods.ts", source), d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void MutableArrayMethodsCrossModuleLinkerAndEmitter()
    {
        const string root = "/virtual/array-methods";
        var host = new Host();
        host.Files[root + "/model.ts"] =
            "export type Numbers = Array<number>; export const values: Numbers = [20,21];";
        host.Files[root + "/logic.ts"] =
            "import type {Numbers} from \"./model.js\"; export function append(xs:Numbers,n:number):number{return xs.push(n);} export function take(xs:Numbers):number|undefined{return xs.shift();}";
        host.Files[root + "/bridge.ts"] =
            "export {values} from \"./model.js\"; export {append,take} from \"./logic.js\";";
        host.Files[root + "/entry.ts"] =
            "import {values,append,take} from \"./bridge.js\"; export const size:number=append(values,22); export const first:number|undefined=take(values);";
        var program = CompilerProgram.Load(host,[root + "/entry.ts"],new CompilerOptions {
            NoLib=true, Target=CompilerTarget.Es2015, Module=CompilerModule.Es2015
        });
        Assert.Empty(program.Analyze().Diagnostics);
        var output = program.EmitJavaScript();
        Assert.Equal(4,output.Count);
        Assert.True(output.Any(js=>js.Text.Contains(".push(",StringComparison.Ordinal)));
        Assert.True(output.Any(js=>js.Text.Contains(".shift(",StringComparison.Ordinal)));
    }
}
