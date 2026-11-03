using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded compiler checks and executed JavaScript, not original TS7 acceptance.
public sealed class ArrayFlatUnionTests
{
    [Theory]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let value:Cell[]=xs.flat(0);""")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let value:(number|string[])[]=xs.flat();""")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let value:(number|string)[]=xs.flat(2);""")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let value:(number|string)[]=xs.flat(3);""")]
    [InlineData("""let xs:(number|string[])[]=[1,["a"]];let value:(number|string)[]=xs.flat();""")]
    [InlineData("""let xs:ReadonlyArray<number[]|string[][]>=[[1],[["a"]]];let value:(number|string[])[]=xs.flat();""")]
    [InlineData("""let xs:(number[]|null)[]=[[1],null];let value:(number|null)[]=xs.flat();""")]
    [InlineData("""let xs:Array<number[]|string[][]>=[[1],[["a"]]];let value:(number|string)[]=xs.flat(2);""")]
    public void DistributesArrayFlatAcrossKnownUnionAlternatives(string source) =>
        Assert.Empty(Compilation.Check("native-flat-union.ts",source));

    [Theory]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let wrong:number[]=xs.flat();""","TS2322")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let wrong:string[]=xs.flat(2);""","TS2322")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let wrong:number[][]=xs.flat(0);""","TS2322")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let wrong:(number|string)[]=xs.flat(1);""","TS2322")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let wrong:Cell[]=xs.flat(2);""","TS2322")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];xs.flat(4);""","TSCS1001")]
    [InlineData("""type Cell=number[]|string[][];let xs:Cell[]=[[1],[["a"]]];let depth:number=2;xs.flat(depth);""","TSCS1001")]
    [InlineData("""let xs:(number|string[])[]=[1,["a"]];let wrong:number[]=xs.flat();""","TS2322")]
    public void RejectsUnsoundUnionFlatteningAndUnboundedDepth(string source,string code) =>
        Assert.Contains(Compilation.Check("invalid-flat-union.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void FourLinkedModulesExecuteBothMixedUnionDepthsWithoutMutatingSource()
    {
        var root=Directory.CreateTempSubdirectory("tscs-flat-union-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Cell=number[]|string[][];export const mixed:Cell[]=[[1,2],[["a"],["b"]]];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Cell} from "./model.js";export function shallow(xs:Cell[]):(number|string[])[]{return xs.flat();}export function deep(xs:Cell[]):(number|string)[]{return xs.flat(2);}export function unchanged(xs:Cell[]):Cell[]{return xs.flat(0);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {mixed} from "./model.js";export {shallow,deep,unchanged} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {mixed,shallow,deep,unchanged} from "./bridge.js";export const one:(number|string[])[]=shallow(mixed);export const two:(number|string)[]=deep(mixed);export const zero:(number[]|string[][])[]=unchanged(mixed);export const original=mixed;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var outputs=program.EmitJavaScript();
            Assert.Equal(4,outputs.Count);
            Assert.Contains(outputs,js=>js.Text.Contains(".flat(",StringComparison.Ordinal));
            foreach(var output in outputs)
                File.WriteAllText(Path.ChangeExtension(output.SourceFile,".js"),output.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {one,two,zero,original} from './entry.js';console.log(JSON.stringify({one,two,zero,original}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Node mixed union flat execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"one":[1,2,["a"],["b"]],"two":[1,2,"a","b"],"zero":[[1,2],[["a"],["b"]]],"original":[[1,2],[["a"],["b"]]]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleUnionFlatteningBlocksAllJavascriptEmission()
    {
        const string root="/virtual/native-flat-union-negative";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const mixed:(number[]|string[][])[]=[[1],[["a"]]];""";
        host.Files[root+"/entry.ts"]=
            """import {mixed} from "./model.js";export const wrong:number[]=mixed.flat();""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
