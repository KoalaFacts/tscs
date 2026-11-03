using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// A bounded compiler/runtime regression, not original TS7 acceptance.
public sealed class ArrayFlatTupleTests
{
    [Theory]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let flat:(number|string[])[]=rows.flat();""")]
    [InlineData("""type Pair=readonly [readonly number[],readonly string[][]];let rows:Pair=[[1],[["a"]]];let flat:(number|string)[]=rows.flat(2);""")]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let flat:(number[]|string[][])[]=rows.flat(0);""")]
    [InlineData("""type Pair=[number[],string[][]];let rows:Pair=[[1],[["a"]]];let flat:(number|string)[]=rows.flat(3);""")]
    [InlineData("""type Pair=readonly [number[],string];let rows:Pair=[[1],"ok"];let flat:(number|string)[]=rows.flat();""")]
    [InlineData("""type Pair=readonly [number[],string[]?];let rows:Pair=[[1]];let flat:(number|string|undefined)[]=rows.flat();""")]
    [InlineData("""const exact=[[21],["hi"]] as const;let flat:(21|"hi")[]=exact.flat();""")]
    [InlineData("""type Nested=readonly [readonly [number,string],readonly [boolean]];let rows:Nested=[[1,"x"],[true]];let flat:(number|string|boolean)[]=rows.flat();""")]
    public void FiniteTuplesFlattenWithPositionAwareElementUnions(string source) =>
        Assert.Empty(Compilation.Check("flat-tuple.ts",source));

    [Theory]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let wrong:number[]=rows.flat();""","TS2322")]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let wrong:(number|string)[]=rows.flat();""","TS2322")]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let wrong:number[]=rows.flat(0);""","TS2322")]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let wrong:boolean[]=rows.flat(2);""","TS2322")]
    [InlineData("""type Pair=readonly [number[],string[]?];let rows:Pair=[[1]];let wrong:number[]=rows.flat();""","TS2322")]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];rows.flat(4);""","TSCS1001")]
    [InlineData("""type Pair=readonly [number[],string[][]];let rows:Pair=[[1],[["a"]]];let depth:number=2;rows.flat(depth);""","TSCS1001")]
    [InlineData("""function f(xs:readonly [number[],string[][]]):void{let extracted=xs.flat;extracted();}""","TSCS1001")]
    public void RejectsUnsafeTupleFlatteningAndUnboundCalls(string source,string expected) =>
        Assert.Contains(Compilation.Check("flat-tuple-invalid.ts",source),d=>d.Code==expected);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void FourLinkedModulesExecuteTupleFlatAtAllSupportedDepths()
    {
        var root=Directory.CreateTempSubdirectory("tscs-flat-tuple-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Rows=readonly [readonly number[],readonly string[][]];export const values:Rows=[[1,2],[["a"],["b"]]];export const optional:readonly [number[],string[]?]=[[7]];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Rows} from "./model.js";export function zero(xs:Rows):(readonly number[]|readonly string[][])[]{return xs.flat(0);}export function one(xs:Rows):(number|string[])[]{return xs.flat();}export function two(xs:Rows):(number|string)[]{return xs.flat(2);}export function optionalFlat(xs:readonly [number[],string[]?]):(number|string|undefined)[]{return xs.flat();}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {values,optional} from "./model.js";export {zero,one,two,optionalFlat} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {values,optional,zero,one,two,optionalFlat} from "./bridge.js";export const shallow:(readonly number[]|readonly string[][])[]=zero(values);export const once:(number|string[])[]=one(values);export const deep:(number|string)[]=two(values);deep[0]=99;export const optionalResult:(number|string|undefined)[]=optionalFlat(optional);export const original=values;""";
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
                """import {shallow,once,deep,optionalResult,original} from './entry.js';console.log(JSON.stringify({shallow,once,deep,optionalResult,original}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Node finite tuple flat execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"shallow":[[1,2],[["a"],["b"]]],"once":[1,2,["a"],["b"]],"deep":[99,2,"a","b"],"optionalResult":[7],"original":[[1,2],[["a"],["b"]]]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedTupleFlatBlocksJavaScriptEmission()
    {
        const string root="/virtual/native-flat-tuple-negative";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly [number[],string[][]]=[[1],[["a"]]];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const wrong:number[]=values.flat();""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
