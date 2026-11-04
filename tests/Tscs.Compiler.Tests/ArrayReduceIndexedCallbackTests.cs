using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Real bounded source checks; never count as original TS7 acceptance.
public sealed class ArrayReduceIndexedCallbackTests
{
    [Theory]
    [InlineData("""let xs:number[]=[2,4];let out:number=xs.reduce((a:number,n:number,i:number):number=>a+n+i,0);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let out:string=xs.reduceRight((a:string,n:number,i:number,source:readonly number[]):string=>a+n+i+source.length,"");""")]
    [InlineData("""let xs:ReadonlyArray<number>=[2,4];let out:string=xs.reduce((a:string,n:number,i:number,source:ReadonlyArray<number>):string=>a+n+i+source.length,"");""")]
    [InlineData("""let xs:number[]=[2,4];let out:number=xs.reduce((a:number,n:number,i:number,source:number[]):number=>a+n+i+source.length);""")]
    [InlineData("""let xs:number[]=[2,4];let out:number=xs.reduce((a:number):number=>a+1,0);""")]
    [InlineData("""let xs:number[]=[2,4];let out:number=xs.reduce(():number=>7,0);""")]
    [InlineData("""let xs:number[]=[2,4];let out:number=xs.reduce((a:number,n:number,i:number,source:readonly number[]):number=>a+n+i+source.length,0);""")]
    [InlineData("""function fold(xs:readonly number[]):number{return xs.reduce((a:number,n:number,i:number,source:readonly number[]):number=>a+n+i+source.length,0);}let out:number=fold([2,4]);""")]
    [InlineData("""function fold(xs:number[]|readonly number[]):number{return xs.reduce((a:number,n:number,i:number,source:readonly number[]):number=>a+n+i+source.length,0);}let out:number=fold([2,4]);""")]
    public void ChecksIndexedAndOriginalArrayArguments(string source) =>
        Assert.Empty(Compilation.Check("reduce-indexed.ts",source));

    [Theory]
    [InlineData("""let xs:number[]=[2];xs.reduce((a:number,n:number,i:string):number=>a+n,0);""","TS2345")]
    [InlineData("""let xs:readonly number[]=[2];xs.reduce((a:number,n:number,i:number,source:number[]):number=>a+n,0);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.reduceRight((a:number,n:number,i:number,source:string[]):number=>a+n,0);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.reduce((a:number,n:number,i:number,source:number[],extra:number):number=>a+n,0);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.reduce((a:string,n:number,i:number):string=>a+n,0);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.reduce((a:number,n:number,i:number):string=>"bad",0);""","TS2345")]
    [InlineData("""let xs:number[]=[2];let wrong:string=xs.reduce((a:number,n:number,i:number):number=>a+n,0);""","TS2322")]
    [InlineData("""let xs:readonly number[]=[2];xs.reduceRight((a:number,n:number,i:string):number=>a+n);""","TS2345")]
    [InlineData("""function fold(xs:number[]|readonly number[]):number{return xs.reduce((a:number,n:number,i:number,source:number[]):number=>a+n,0);}""","TS2345")]
    public void RejectsInvalidIndexedFoldCallbacks(string source,string expected) =>
        Assert.Contains(Compilation.Check("reduce-indexed-invalid.ts",source),d=>d.Code==expected);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void EmitsAndExecutesIndexedFoldsAcrossFourESModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-reduce-indexed-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Values=readonly number[];export const values:Values=[2,4,6];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Values} from "./model.js";""" +
                """export function forward(xs:Values):string{return xs.reduce((a:string,n:number,i:number,source:readonly number[]):string=>a+n+":"+i+":"+source.length+";","");}""" +
                """export function backward(xs:Values):number{return xs.reduceRight((a:number,n:number,i:number,source:readonly number[]):number=>a*10+i,0);}""" +
                """export function unseeded(xs:Values):number{return xs.reduce((a:number,n:number,i:number,source:readonly number[]):number=>a+n+i+source.length);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {values} from "./model.js";export {forward,backward,unseeded} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {values,forward,backward,unseeded} from "./bridge.js";""" +
                """export const label:string=forward(values);export const reverse:number=backward(values);""" +
                """export const sum:number=unseeded(values);export const original=values;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var output=program.EmitJavaScript();
            Assert.Equal(4,output.Count);
            foreach(var item in output)
                File.WriteAllText(Path.ChangeExtension(item.SourceFile,".js"),item.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {label,reverse,sum,original} from './entry.js';console.log(JSON.stringify({label,reverse,sum,original}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Indexed array fold execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"label":"2:0:3;4:1:3;6:2:3;","reverse":210,"sum":21,"original":[2,4,6]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleFoldDoesNotEmitJavaScript()
    {
        const string root="/virtual/reduce-indexed-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly number[]=[2,4];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const bad=values.reduce((a:number,n:number,i:number,source:number[]):number=>a+n,0);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
