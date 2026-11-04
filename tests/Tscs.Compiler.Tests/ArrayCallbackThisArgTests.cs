using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded compiler behavior; these are not original TS7 acceptance tests.
public sealed class ArrayCallbackThisArgTests
{
    [Theory]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let out:number[]=xs.map((n:number,i:number):number=>n+i+ctx.bias,ctx);""")]
    [InlineData("""let xs:number[]=[2,4];let ctx={bias:3};let out:number[]=xs.flatMap((n:number):number[]=>[n,n+ctx.bias],ctx);""")]
    [InlineData("""let xs:ReadonlyArray<number>=[2,4];let ctx={bias:3};let out:number[]=xs.filter((n:number):boolean=>n>ctx.bias,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let yes:boolean=xs.some((n:number):boolean=>n===4,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let yes:boolean=xs.every((n:number):boolean=>n>0,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let n:number|undefined=xs.find((n:number):boolean=>n>ctx.bias,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let n:number|undefined=xs.findLast((n:number):boolean=>n>ctx.bias,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let i:number=xs.findIndex((n:number):boolean=>n>ctx.bias,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let i:number=xs.findLastIndex((n:number):boolean=>n>ctx.bias,ctx);""")]
    [InlineData("""let xs:readonly number[]=[2,4];let ctx={bias:3};let total:number=0;xs.forEach((n:number):void=>{total=total+n+ctx.bias;},ctx);""")]
    [InlineData("""function map(xs:number[]|readonly number[]):number[]{let ctx={bias:3};return xs.map((n:number):number=>n+ctx.bias,ctx);}""")]
    [InlineData("""function flat(xs:number[]|readonly number[]):number[]{let ctx={bias:3};return xs.flatMap((n:number):number[]=>[n+ctx.bias],ctx);}""")]
    [InlineData("""type MapFn=((n:number)=>number)|((n:number)=>number);function apply(xs:number[],cb:MapFn):number[]{return xs.map(cb,{bias:3});}""")]
    [InlineData("""let xs:number[]=[2];let out:number[]=xs.map((n:number):number=>n,undefined);""")]
    public void OptionalThisArgumentPreservesCheckedNativeTypes(string source) =>
        Assert.Empty(Compilation.Check("array-callback-thisarg.ts",source));

    [Theory]
    [InlineData("""let xs:number[]=[2];xs.map((n:number):number=>n,{bias:2},{bias:3});""","TS2554")]
    [InlineData("""let xs:number[]=[2];xs.flatMap((n:number):number[]=>[n],{},{});""","TS2554")]
    [InlineData("""let xs:number[]=[2];xs.filter((n:number):boolean=>true,{},{});""","TS2554")]
    [InlineData("""let xs:number[]=[2];xs.forEach((n:number):void=>{}, {}, {});""","TS2554")]
    [InlineData("""let xs:number[]=[2];xs.reduce((a:number,n:number):number=>a+n,0,{bias:3});""","TS2554")]
    [InlineData("""let xs:readonly number[]=[2];xs.map((n:number,i:number,source:number[]):number=>n,{bias:3});""","TS2345")]
    [InlineData("""let xs:number[]=[2];let bad:string[]=xs.map((n:number):number=>n,{bias:3});""","TS2322")]
    [InlineData("""let xs:number[]=[2];let bad:string[]=xs.flatMap((n:number):number[]=>[n],{bias:3});""","TS2322")]
    [InlineData("""let xs:number[]=[2];let bad:number[]=xs.flatMap((n:number):string[]=>["x"],{bias:3});""","TS2322")]
    [InlineData("""let xs:readonly number[]=[2];let bad:string=xs.find((n:number):boolean=>true,{bias:3});""","TS2322")]
    public void InvalidCallbacksReturnsAndArityStillReject(string source,string diagnostic) =>
        Assert.Contains(Compilation.Check("array-callback-thisarg-invalid.ts",source),
            d=>d.Code==diagnostic);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void NativeCallbacksWithThisArgExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-callback-thisarg-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Values=ReadonlyArray<number>;export const values:Values=[2,4,6];export const ctx={bias:3};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Values} from "./model.js";""" +
                """export function mapped(xs:Values,ctx:{bias:number}):number[]{return xs.map((n:number,i:number):number=>n+i+ctx.bias,ctx);}""" +
                """export function flat(xs:Values,ctx:{bias:number}):number[]{return xs.flatMap((n:number):number[]=>[n,n+ctx.bias],ctx);}""" +
                """export function filtered(xs:Values,ctx:{bias:number}):number[]{return xs.filter((n:number):boolean=>n>ctx.bias,ctx);}""" +
                """export function any(xs:Values,ctx:{bias:number}):boolean{return xs.some((n:number):boolean=>n===4,ctx);}""" +
                """export function all(xs:Values,ctx:{bias:number}):boolean{return xs.every((n:number):boolean=>n>0,ctx);}""" +
                """export function first(xs:Values,ctx:{bias:number}):number|undefined{return xs.find((n:number):boolean=>n>ctx.bias,ctx);}""" +
                """export function last(xs:Values,ctx:{bias:number}):number|undefined{return xs.findLast((n:number):boolean=>n>ctx.bias,ctx);}""" +
                """export function firstIndex(xs:Values,ctx:{bias:number}):number{return xs.findIndex((n:number):boolean=>n===4,ctx);}""" +
                """export function lastIndex(xs:Values,ctx:{bias:number}):number{return xs.findLastIndex((n:number):boolean=>n===4,ctx);}""" +
                """export function visited(xs:Values,ctx:{bias:number}):number{let total:number=0;xs.forEach((n:number):void=>{total=total+n+ctx.bias;},ctx);return total;}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {values,ctx} from "./model.js";""" +
                """export {mapped,flat,filtered,any,all,first,last,firstIndex,lastIndex,visited} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {values,ctx,mapped,flat,filtered,any,all,first,last,firstIndex,lastIndex,visited} from "./bridge.js";""" +
                """export const mapOut:number[]=mapped(values,ctx);export const flatOut:number[]=flat(values,ctx);""" +
                """export const filterOut:number[]=filtered(values,ctx);export const anyOut:boolean=any(values,ctx);""" +
                """export const allOut:boolean=all(values,ctx);export const firstOut:number|undefined=first(values,ctx);""" +
                """export const lastOut:number|undefined=last(values,ctx);export const firstIndexOut:number=firstIndex(values,ctx);""" +
                """export const lastIndexOut:number=lastIndex(values,ctx);export const totalOut:number=visited(values,ctx);""" +
                """export const source=values;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {
                    NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015
                });
            Assert.Empty(program.Analyze().Diagnostics);
            var output=program.EmitJavaScript();
            Assert.Equal(4,output.Count);
            Assert.Contains(output,js=>js.Text.Contains(".flatMap(",StringComparison.Ordinal));
            Assert.Contains(output,js=>js.Text.Contains(".map(",StringComparison.Ordinal));
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
                """import {mapOut,flatOut,filterOut,anyOut,allOut,firstOut,lastOut,firstIndexOut,lastIndexOut,totalOut,source} from './entry.js';console.log(JSON.stringify({mapOut,flatOut,filterOut,anyOut,allOut,firstOut,lastOut,firstIndexOut,lastIndexOut,totalOut,source}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Native callback execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"mapOut":[5,8,11],"flatOut":[2,5,4,7,6,9],"filterOut":[4,6],"anyOut":true,"allOut":true,"firstOut":4,"lastOut":6,"firstIndexOut":1,"lastIndexOut":1,"totalOut":21,"source":[2,4,6]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleCallbackReturnPreventsAnyJavaScriptOutput()
    {
        const string root="/virtual/array-callback-thisarg-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly number[]=[2,4];export const ctx={bias:3};""";
        host.Files[root+"/entry.ts"]=
            """import {values,ctx} from "./model.js";export const wrong:string[]=values.map((n:number):number=>n+ctx.bias,ctx);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
