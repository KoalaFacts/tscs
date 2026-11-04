using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed bounded intrinsic tests, NOT original TS7 acceptance.
public sealed class ArrayIsArrayGuardTests
{
    [Theory]
    [InlineData("""function f(v:number[]|string):void{if(Array.isArray(v)){let a:number[]=v;a.push(1);}else{let s:string=v;}}""")]
    [InlineData("""function f(v:readonly number[]|string):number{if(Array.isArray(v)){let a:readonly number[]=v;return a.length;}let s:string=v;return s.length;}""")]
    [InlineData("""function f(v:number[]|null):number{if(!Array.isArray(v)){let n:null=v;return 0;}let a:number[]=v;return a.length;}""")]
    [InlineData("""function f(v:readonly [number,string]|number):string{if(Array.isArray(v)){let a:readonly [number,string]=v;return a[1];}return "none";}""")]
    [InlineData("""function f(v:number[]|string):number{if(Array.isArray(v)&&v.length>0){return v[0];}return 0;}""")]
    [InlineData("""function f(v:number[]|string):void{if(!Array.isArray(v)){let s:string=v;}else{let a:number[]=v;}}""")]
    [InlineData("""function f(v:number[]|string):boolean{return Array.isArray(v);}""")]
    [InlineData("""let Array:{isArray:(n:number)=>number}={isArray:(n:number):number=>n+1};let result:number=Array.isArray(2);""")]
    public void ChecksArrayIsArrayWithoutChangingOrdinaryShadowedCallables(string source) =>
        Assert.Empty(Compilation.Check("array-isarray.ts",source));

    [Theory]
    [InlineData("""function f(v:number[]|string):void{if(Array.isArray(v)){let bad:string=v;}}""","TS2322")]
    [InlineData("""function f(v:number[]|string):void{if(Array.isArray(v)){}else{let bad:number[]=v;}}""","TS2322")]
    [InlineData("""function f(v:readonly number[]|string):void{if(Array.isArray(v)){v.push(1);}}""","TS2339")]
    [InlineData("""function f(v:number[]|{length:number}):void{if(Array.isArray(v)){let bad:number[]=v;}}""","TS2322")]
    [InlineData("""function f(v:number[]|string,Array:{isArray:(x:number[]|string)=>boolean}):void{if(Array.isArray(v)){let bad:number[]=v;}}""","TS2322")]
    [InlineData("""function f(Array:boolean=Array.isArray(1)):void{}f();""","TSCS1001")]
    [InlineData("""let f=(Array:boolean=Array.isArray(1)):boolean=>Array;""","TSCS1001")]
    [InlineData("""function f(value:number=Array.isArray(1),Array:number=2):void{}""","TSCS1001")]
    [InlineData("""function f(value:boolean=Array.isArray(1),Array:boolean=Array.isArray(2)):void{}""","TSCS1001")]
    [InlineData("""let flag:boolean=Array.isArray();""","TS2554")]
    [InlineData("""let flag:boolean=Array.isArray(1,2);""","TS2554")]
    [InlineData("""let flag:number=Array.isArray([1,2]);""","TS2322")]
    [InlineData("""let values:number[]=[1];let flag:boolean=Array.isArray(...values);""","TSCS1001")]
    public void RejectsUnsafePartitionsArityAndResults(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("array-isarray-negative.ts",source),
            d=>d.Code==diagnostic);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void ArrayIsArrayNarrowingRunsAcrossFourRealEmittedModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-array-guard-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Payload=number[]|string|null;export const numbers:Payload=[4,5];export const label:Payload="hi";export const absent:Payload=null;""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Payload} from "./model.js";""" +
                """export function total(v:Payload):number{if(Array.isArray(v)){let a:number[]=v;return a[0]+a[1];}return 0;}""" +
                """export function classify(v:Payload):string{if(Array.isArray(v)){return "array";}return "other";}""" +
                """export function isArray(v:Payload):boolean{return Array.isArray(v);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {numbers,label,absent} from "./model.js";""" +
                """export {total,classify,isArray} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {numbers,label,absent,total,classify,isArray} from "./bridge.js";""" +
                """export const answer:number=total(numbers);export const kind:string=classify(label);""" +
                """export const missing:string=classify(absent);export const checked:boolean=isArray(numbers);""" +
                """export const source=numbers;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,js=>js.Text.Contains("Array.isArray(",StringComparison.Ordinal));
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
                """import {answer,kind,missing,checked,source} from './entry.js';console.log(JSON.stringify({answer,kind,missing,checked,source}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Array.isArray emitted runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"answer":9,"kind":"other","missing":"other","checked":true,"source":[4,5]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleArrayGuardPreventsJavaScriptOutput()
    {
        const string root="/virtual/array-isarray-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export type Payload=number[]|string;export const values:Payload=[1,2];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const wrong:string=Array.isArray(values);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
