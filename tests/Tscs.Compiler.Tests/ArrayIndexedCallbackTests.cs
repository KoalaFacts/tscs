using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Product source/runtime checks, not original TS7 acceptance credits.
public sealed class ArrayIndexedCallbackTests
{
    [Theory]
    [InlineData("""let xs:number[]=[2,4];let out:number[]=xs.map((n:number,i:number):number=>n+i);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let out:number[]=xs.map((n:number,i:number,a:readonly number[]):number=>n+i+a.length);""")]
    [InlineData("""let xs:number[]=[2,4];let out:number[]=xs.flatMap((n:number,i:number):number[]=>[n,i]);""")]
    [InlineData("""let xs:ReadonlyArray<number>=[2,4];let out:number[]=xs.flatMap((n:number,i:number,a:ReadonlyArray<number>):number[]=>[n+i,a.length]);""")]
    [InlineData("""let xs:number[]=[2,4];let out:number[]=xs.filter((n:number,i:number):boolean=>n>i);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let out:number[]=xs.filter((n:number,i:number,a:readonly number[]):boolean=>a[i]===n);""")]
    [InlineData("""let xs:number[]=[2,4];let yes:boolean=xs.some((n:number,i:number):boolean=>n===i);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let yes:boolean=xs.every((n:number,i:number,a:readonly number[]):boolean=>a[i]===n);""")]
    [InlineData("""let xs:number[]=[2,4];let found:number|undefined=xs.find((n:number,i:number):boolean=>i===1);""")]
    [InlineData("""let xs:number[]=[2,4];let found:number=xs.findIndex((n:number,i:number):boolean=>n===i);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let found:number|undefined=xs.findLast((n:number,i:number):boolean=>i===0);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let found:number=xs.findLastIndex((n:number,i:number):boolean=>n>i);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let total:number=0;xs.forEach((n:number,i:number,a:readonly number[]):void=>{total=total+n+i+a.length;});""")]
    [InlineData("""let xs:number[]=[2,4];let out:number[]=xs.map(():number=>7);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let yes:boolean=xs.some(():boolean=>true);""")]
    [InlineData("""let xs:number[]=[2,4];let out:number[]=xs.filter((n:number):boolean=>n>2);""")]
    public void CheckedNativeCallbacksAcceptZeroToThreeParameters(string source) =>
        Assert.Empty(Compilation.Check("indexed-array-callbacks.ts",source));

    [Theory]
    [InlineData("""function f(xs:number[]|readonly number[]):boolean{return xs.some((n:number):boolean=>n>0);}""")]
    [InlineData("""function f(xs:readonly number[]|number[]):boolean{return xs.every((n:number,i:number):boolean=>n>=i);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number|undefined{return xs.find((n:number,i:number,a:readonly number[]):boolean=>a[i]===n);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number{return xs.findIndex((n:number):boolean=>n>1);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number|undefined{return xs.findLast((n:number,i:number):boolean=>i===0);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number{return xs.findLastIndex((n:number,i:number):boolean=>i===0);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number[]{return xs.filter((n:number,i:number):boolean=>n>=i);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number[]{return xs.map((n:number,i:number,a:readonly number[]):number=>n+i+a.length);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number[]{return xs.flatMap((n:number,i:number):number[]=>[n,i]);}""")]
    [InlineData("""function f(xs:number[]|readonly number[]):number{let sum:number=0;xs.forEach((n:number,i:number,a:readonly number[]):void=>{sum=sum+n+i+a.length;});return sum;}""")]
    [InlineData("""function f(xs:number[]|string[]):boolean{return xs.some((n:number|string):boolean=>true);}""")]
    public void ArrayUnionCallbacksKeepTheSharedSafeSignature(string source) =>
        Assert.Empty(Compilation.Check("array-union-callbacks.ts",source));

    [Theory]
    [InlineData("""function f(xs:number[]|readonly number[]):boolean{return xs.some((n:number,i:number,a:number[]):boolean=>true);}""","TS2345")]
    [InlineData("""function f(xs:number[]|readonly number[]):number[]{return xs.map((n:number,i:number,a:number[]):number=>n);}""","TS2345")]
    [InlineData("""function f(xs:number[]|string[]):boolean{return xs.every((n:number):boolean=>true);}""","TS2345")]
    public void ArrayUnionCallbacksRejectUnsafeNarrowing(string source,string expected) =>
        Assert.Contains(Compilation.Check("array-union-callback-invalid.ts",source),d=>d.Code==expected);

    [Theory]
    [InlineData("""let xs:number[]=[2];xs.map((n:number,i:string):number=>n);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.flatMap((n:number,i:boolean):number[]=>[n]);""","TS2345")]
    [InlineData("""let xs:readonly number[]=[2];xs.filter((n:number,i:number,a:number[]):boolean=>true);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.some((n:number,i:boolean):boolean=>true);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.every((n:number,i:number,a:string[]):boolean=>true);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.find((n:number,i:number,a:readonly number[],extra:number):boolean=>true);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.findIndex((n:number,i:string):boolean=>true);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.findLast((n:number,i:boolean):boolean=>true);""","TS2345")]
    [InlineData("""let xs:number[]=[2];xs.findLastIndex((n:number,i:number,a:string[]):boolean=>true);""","TS2345")]
    [InlineData("""let xs:readonly number[]=[2];xs.forEach((n:number,i:number,a:number[]):void=>{});""","TS2345")]
    [InlineData("""let xs:number[]=[2];let wrong:string[]=xs.map((n:number,i:number):number=>n+i);""","TS2322")]
    [InlineData("""let xs:number[]=[2];let wrong:string[]=xs.flatMap((n:number,i:number):number[]=>[n+i]);""","TS2322")]
    public void InvalidCallbackPositionsAndArrayTypesAreRejected(string source,string expected) =>
        Assert.Contains(Compilation.Check("indexed-callback-invalid.ts",source),d=>d.Code==expected);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void IndexedCallbacksExecuteAcrossFourLinkedModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-indexed-callbacks-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Values=readonly number[];export const values:Values=[2,4,6];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Values} from "./model.js";""" +
                """export function map(xs:Values):string[]{return xs.map((n:number,i:number,source:readonly number[]):string=>"v"+n+":"+i);}""" +
                """export function select(xs:Values):number[]{return xs.filter((n:number,i:number,source:readonly number[]):boolean=>i>0 && source.length===3);}""" +
                """export function expand(xs:Values):number[]{return xs.flatMap((n:number,i:number,source:readonly number[]):number[]=>[n+i,source.length]);}""" +
                """export function last(xs:Values):number|undefined{return xs.findLast((n:number,i:number):boolean=>i===1);}""" +
                """export function visit(xs:Values):number{let total:number=0;xs.forEach((n:number,i:number,source:readonly number[]):void=>{total=total+n+i+source.length;});return total;}""" +
                """export function any(xs:Values):boolean{return xs.some((n:number,i:number):boolean=>n===i+2);}""" +
                """export function all(xs:Values):boolean{return xs.every((n:number,i:number,source:readonly number[]):boolean=>n===source[i]);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {values} from "./model.js";export {map,select,expand,last,visit,any,all} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {values,map,select,expand,last,visit,any,all} from "./bridge.js";""" +
                """export const mapped:string[]=map(values);export const filtered:number[]=select(values);export const expanded:number[]=expand(values);""" +
                """export const right:number|undefined=last(values);export const total:number=visit(values);""" +
                """export const hasAny:boolean=any(values);export const hasAll:boolean=all(values);export const original=values;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var output=program.EmitJavaScript();
            Assert.Equal(4,output.Count);
            foreach(var entry in output)
                File.WriteAllText(Path.ChangeExtension(entry.SourceFile,".js"),entry.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                """import {mapped,filtered,expanded,right,total,hasAny,hasAll,original} from './entry.js';console.log(JSON.stringify({mapped,filtered,expanded,right,total,hasAny,hasAll,original}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Indexed array callbacks exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"mapped":["v2:0","v4:1","v6:2"],"filtered":[4,6],"expanded":[2,3,5,3,8,3],"right":4,"total":24,"hasAny":true,"hasAll":true,"original":[2,4,6]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedCallbackStopsAllJavaScriptOutput()
    {
        const string root="/virtual/indexed-callback-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly number[]=[2,4];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const wrong=values.map((n:number,i:string):number=>n);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {
                NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
            });
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2345");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
