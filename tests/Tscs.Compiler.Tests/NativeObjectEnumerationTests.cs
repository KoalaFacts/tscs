using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded ES2017 native Object enumeration, not original TS7 acceptance.
public sealed class NativeObjectEnumerationTests
{
    [Theory]
    [InlineData("""let names:string[]=Object.keys({id:1,label:"koala"});""")]
    [InlineData("""let names:string[]=Object.keys({id:1,nested:{value:4}});""")]
    [InlineData("""let names:string[]=Object.keys({});""")]
    [InlineData("""let names:string[]=Object.keys([2,4]);""")]
    [InlineData("""let values:number[]=Object.values({a:2,b:4});""")]
    [InlineData("""let values:number[]=Object.values([2,4]);""")]
    [InlineData("""function f(values:readonly number[]):number[]{return Object.values(values);}""")]
    [InlineData("""let entries:[string,number][]=Object.entries({a:2,b:4});""")]
    [InlineData("""let entries:[string,number][]=Object.entries([2,4]);""")]
    [InlineData("""function f(Object:{keys:(n:number)=>number}):number{return Object.keys(7);}""")]
    public void AcceptsCheckedNativeEnumeration(string source) =>
        Assert.Empty(Compilation.Check("object-enumeration.ts",source));

    [Theory]
    [InlineData("""let names=Object.keys();""","TS2554")]
    [InlineData("""let names=Object.keys({},{});""","TS2554")]
    [InlineData("""let names=Object.keys(...[2]);""","TSCS1001")]
    [InlineData("""let names=Object.keys(null);""","TSCS1001")]
    [InlineData("""let names=Object.keys(42);""","TSCS1001")]
    [InlineData("""let values=Object.values({a:2,b:"mixed"});""","TSCS1001")]
    [InlineData("""let values=Object.values({});""","TSCS1001")]
    [InlineData("""let entries=Object.entries({a:2,b:"mixed"});""","TSCS1001")]
    [InlineData("""let entries=Object.entries(undefined);""","TSCS1001")]
    [InlineData("""let bad:number[]=Object.keys({a:2});""","TS2322")]
    [InlineData("""let bad:string[]=Object.values({a:2});""","TS2322")]
    [InlineData("""let bad:[number,string][]=Object.entries({a:2});""","TS2322")]
    [InlineData("""function f(Object:{keys:(n:number)=>number}):number{return Object.keys("bad");}""","TS2345")]
    public void RejectsUnsafeEnumeration(string source,string code) =>
        Assert.Contains(Compilation.Check("object-enumeration-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void EmittedEnumerationExecutesAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-object-enum-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const record:{first:number,last:number}={first:10,last:12};""" +
                """export const list:readonly number[]=[2,4];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """export function names(item:{first:number,last:number}):string[]{return Object.keys(item);}""" +
                """export function values(item:{first:number,last:number}):number[]{return Object.values(item);}""" +
                """export function pairs(item:{first:number,last:number}):[string,number][]{return Object.entries(item);}""" +
                """export function copy(items:readonly number[]):number[]{return Object.values(items);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {record,list} from "./model.js";""" +
                """export {names,values,pairs,copy} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {record,list,names,values,pairs,copy} from "./bridge.js";""" +
                """export const keys:string[]=names(record);export const numbers:number[]=values(record);""" +
                """export const rows:[string,number][]=pairs(record);export const array:number[]=copy(list);""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,item=>item.Text.Contains("Object.keys(",StringComparison.Ordinal));
            Assert.Contains(emitted,item=>item.Text.Contains("Object.values(",StringComparison.Ordinal));
            Assert.Contains(emitted,item=>item.Text.Contains("Object.entries(",StringComparison.Ordinal));
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
                """import {keys,numbers,rows,array} from './entry.js';console.log(JSON.stringify({keys,numbers,rows,array}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Native Object enumeration execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"keys":["first","last"],"numbers":[10,12],"rows":[["first",10],["last",12]],"array":[2,4]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleEnumerationPreventsJavaScriptOutput()
    {
        const string root="/virtual/object-enumeration-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const record:{first:number,last:number}={first:10,last:12};""";
        host.Files[root+"/entry.ts"]=
            """import {record} from "./model.js";export const invalid:string[]=Object.values(record);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
