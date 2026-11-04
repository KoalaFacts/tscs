using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded native Array.of source/runtime acceptance; not original TS7 PASS.
public sealed class ArrayOfNativeTests
{
    [Theory]
    [InlineData("""let xs:number[]=Array.of(2,4,6);xs.push(8);""")]
    [InlineData("""let xs:string[]=Array.of("koala","facts");xs.push("bakery");""")]
    [InlineData("""let xs:boolean[]=Array.of(true,false);""")]
    [InlineData("""let xs=Array.of();xs.push(2);""")]
    [InlineData("""let xs:(number|undefined)[]=Array.of(1,undefined);""")]
    [InlineData("""let xs:number[]=Array.of(...[2,4]);""")]
    [InlineData("""let source:readonly number[]=[2,4];let xs:number[]=Array.of(1,...source);xs.push(8);""")]
    [InlineData("""let xs:number[][]=Array.of([2],[4,6]);""")]
    [InlineData("""let xs:{id:number}[]=Array.of({id:2},{id:4});""")]
    [InlineData("""function make(Array:{of:(n:number)=>number}):number{return Array.of(42);}""")]
    public void ChecksNativeArrayOfValuesAndFreshMutableResults(string source) =>
        Assert.Empty(Compilation.Check("array-of.ts",source));

    [Theory]
    [InlineData("""let xs:number[]=Array.of(2,"wrong");""","TS2345")]
    [InlineData("""let xs:string[]=Array.of(2,4);""","TS2322")]
    [InlineData("""let xs:number[]=Array.of("koala","facts");""","TS2322")]
    [InlineData("""let xs:number[]=Array.of(...["a","b"]);""","TS2322")]
    [InlineData("""let xs:number[]=Array.of(1,...["a","b"]);""","TS2345")]
    [InlineData("""let xs:number[]=Array.of(...42);""","TSCS1001")]
    [InlineData("""let xs:number[]=Array.of([2,4]);""","TS2322")]
    [InlineData("""let xs:number[]=Array.of(1,undefined);""","TS2322")]
    [InlineData("""let xs=Array.of();let numbers:number[]=xs;""","TS2322")]
    [InlineData("""function make(Array:{of:(n:number)=>number}):number{return Array.of("wrong");}""","TS2345")]
    public void RejectsUnsafeElementTypesAndInvalidSpreads(string source,string code) =>
        Assert.Contains(Compilation.Check("array-of-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ArrayOfEmitsAndExecutesAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-array-of-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const numbers:readonly number[]=[2,4];export const words:readonly string[]=["koala","facts"];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """export function prepend(n:number,tail:readonly number[]):number[]{return Array.of(n,...tail);}""" +
                """export function tag(prefix:string,tail:readonly string[]):string[]{return Array.of(prefix,...tail);}""" +
                """export function empty():unknown[]{return Array.of();}""" +
                """export function records():{value:number}[]{return Array.of({value:2},{value:4});}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {numbers,words} from "./model.js";""" +
                """export {prepend,tag,empty,records} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {numbers,words,prepend,tag,empty,records} from "./bridge.js";""" +
                """export const made:number[]=prepend(7,numbers);made.push(9);""" +
                """export const names:string[]=tag("hi",words);export const zero:unknown[]=empty();""" +
                """export const rows:{value:number}[]=records();""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,js=>js.Text.Contains("Array.of(",StringComparison.Ordinal));
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
                """import {made,names,zero,rows} from './entry.js';console.log(JSON.stringify({made,names,zero,rows}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Native Array.of execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"made":[7,2,4,9],"names":["hi","koala","facts"],"zero":[],"rows":[{"value":2},{"value":4}]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleArrayOfPreventsJavaScriptOutput()
    {
        const string root="/virtual/array-of-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly number[]=[2,4];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const wrong:string[]=Array.of(7,...values);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
