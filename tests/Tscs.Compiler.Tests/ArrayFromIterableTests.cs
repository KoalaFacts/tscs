using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded intrinsic checks; not original TS7 acceptance.
public sealed class ArrayFromIterableTests
{
    [Theory]
    [InlineData("""let xs:number[]=[2,4];let copy:number[]=Array.from(xs);copy.push(9);""")]
    [InlineData("""let out:number[]=Array.from([1],(n:number):number=>n);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let copy:number[]=Array.from(xs);copy.push(9);""")]
    [InlineData("""let xs:ReadonlyArray<string>=["a","b"];let copy:string[]=Array.from(xs);""")]
    [InlineData("""let xs:readonly [number,string]=[2,"b"];let copy:(number|string)[]=Array.from(xs);""")]
    [InlineData("""let copy:string[]=Array.from("A🚀");""")]
    [InlineData("""function copy(v:number[]|readonly number[]):number[]{return Array.from(v);}""")]
    [InlineData("""function copy(v:string|readonly string[]):string[]{return Array.from(v);}""")]
    [InlineData("""function copy(v:readonly number[][]):number[][]{return Array.from(v);}""")]
    [InlineData("""function f(Array:{from:(n:number)=>number}):number{return Array.from(2);}""")]
    public void CheckedFiniteIterablesReturnWritableNativeArrays(string source) =>
        Assert.Empty(Compilation.Check("array-from.ts",source));

    [Theory]
    [InlineData("""let out:number[]=Array.from();""","TS2554")]
    [InlineData("""let out:number[]=Array.from(42);""","TSCS1001")]
    [InlineData("""let out:number[]=Array.from(null);""","TSCS1001")]
    [InlineData("""function copy(x:number[]|null):number[]{return Array.from(x);}""","TSCS1001")]
    [InlineData("""let x:number[]=[1];let out:number[]=Array.from(...x);""","TSCS1001")]
    [InlineData("""function copy(v:number[]|string):(number|string)[]{return Array.from(v);}""","TSCS1001")]
    [InlineData("""function copy(v:readonly [number,string]|string):(number|string)[]{return Array.from(v);}""","TSCS1001")]
    [InlineData("""let x:number[]=[1];let out:string[]=Array.from(x);""","TS2322")]
    [InlineData("""let x:readonly number[]=[1];let out=Array.from(x);x.push(2);""","TS2339")]
    [InlineData("""function f(Array:{from:(n:number)=>number}):number{return Array.from("x");}""","TS2345")]
    public void UnsupportedSourcesAndUnsafeTypesReject(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("array-from-invalid.ts",source),
            d=>d.Code==diagnostic);

    [Fact]
    public void OversizedIterableUnionsProduceADiagnosticInsteadOfThrowing()
    {
        var first=string.Join("|",Enumerable.Range(0,33).Select(i=>$"\"left{i}\""));
        var second=string.Join("|",Enumerable.Range(0,33).Select(i=>$"\"right{i}\""));
        var source=$"type A={first};type B={second};function f(xs:A[]|B[]):void{{Array.from(xs);}}";
        var diagnostics=Compilation.Check("array-from-large-union.ts",source);
        Assert.Contains(diagnostics,d=>d.Code=="TSCS1001");
    }

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void NativeArrayFromExecutesAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-array-from-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Pair=readonly [number,string];export const pair:Pair=[7,"k"];""" +
                """export const numbers:readonly number[]=[2,4,6];export const text:string="A🚀";""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Pair} from "./model.js";""" +
                """export function copied(xs:readonly number[]):number[]{return Array.from(xs);}""" +
                """export function letters(s:string):string[]{return Array.from(s);}""" +
                """export function mixed(t:Pair):(number|string)[]{return Array.from(t);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {numbers,text,pair} from "./model.js";""" +
                """export {copied,letters,mixed} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {numbers,text,pair,copied,letters,mixed} from "./bridge.js";""" +
                """export const values:number[]=copied(numbers);""" +
                """export const characters:string[]=letters(text);""" +
                """export const pairValues:(number|string)[]=mixed(pair);""" +
                """values.push(9);export const source=numbers;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,js=>js.Text.Contains("Array.from(",StringComparison.Ordinal));
            foreach(var item in emitted)
                File.WriteAllText(Path.ChangeExtension(item.SourceFile,".js"),item.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {values,characters,pairValues,source} from './entry.js';console.log(JSON.stringify({values,characters,pairValues,source}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Native Array.from execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"values":[2,4,6,9],"characters":["A","🚀"],"pairValues":[7,"k"],"source":[2,4,6]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleArrayFromPreventsJavaScriptOutput()
    {
        const string root="/virtual/array-from-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly number[]=[2,4];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const wrong:string[]=Array.from(values);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
