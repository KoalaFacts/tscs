using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded product integration: not an original TS7 compatibility PASS.
public sealed class ArrayFromMappingTests
{
    [Theory]
    [InlineData("""let xs:readonly number[]=[2,4];let out:string[]=Array.from(xs,(n:number,i:number):string=>"v"+(n+i));""")]
    [InlineData("""let xs:number[]=[1,2];let out:number[]=Array.from(xs,(n:number):number=>n*2);""")]
    [InlineData("""let xs:readonly [number,string]=[1,"x"];let out:string[]=Array.from(xs,(v:number|string):string=>"item");""")]
    [InlineData("""let out:string[]=Array.from("A🚀",(s:string,i:number):string=>s+i);""")]
    [InlineData("""let out:number[]=Array.from([1,2],():number=>7);""")]
    [InlineData("""function twice(n:number,index:number):number{return n*2+index;}let out:number[]=Array.from([2,4],twice);""")]
    [InlineData("""let out:number[]=Array.from([2,4],(n:number):number=>n+1,{offset:3});""")]
    [InlineData("""let out:number[]=Array.from([2],(n:number):number=>n,42);""")]
    [InlineData("""let out:number[]=Array.from([2],(n:number):number=>n,undefined);""")]
    [InlineData("""function copy(xs:readonly number[]):number[]{return Array.from(xs,(n:number):number=>n+1);}""")]
    public void MapsCheckedIterableSourcesToExplicitOutputTypes(string source) =>
        Assert.Empty(Compilation.Check("array-from-map.ts",source));

    [Theory]
    [InlineData("""let out:number[]=Array.from([1],42);""","TS2345")]
    [InlineData("""let out:number[]=Array.from([1],(s:string):number=>s.length);""","TS2345")]
    [InlineData("""let out:number[]=Array.from([1],(n:number,index:string):number=>n);""","TS2345")]
    [InlineData("""let out:number[]=Array.from([1],(n:number,i:number,source:number[]):number=>n);""","TS2345")]
    [InlineData("""let out:number[]=Array.from([1],(n:number,...args:number[]):number=>n);""","TS2345")]
    [InlineData("""let out:string[]=Array.from([1],(n:number):number=>n);""","TS2322")]
    [InlineData("""let out:number[]=Array.from([1],(n:number):number=>n,{},{});""","TS2554")]
    [InlineData("""let out:number[]=Array.from(42,(n:number):number=>n);""","TSCS1001")]
    [InlineData("""let out:number[]=Array.from([1],undefined);""","TS2345")]
    [InlineData("""let out:number[]=Array.from([1],(n:number):number=>n);out[0]="bad";""","TS2322")]
    public void RejectsInvalidMapperSourcesArgumentsAndAssignments(string source,string code) =>
        Assert.Contains(Compilation.Check("array-from-map-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void MappersExecuteNativeJavaScriptAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-array-from-map-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export type Pair=readonly [number,string];export const values:readonly number[]=[2,4,6];""" +
                """export const pair:Pair=[7,"k"];export const text:string="A🚀";export const ctx={bias:3};""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import type {Pair} from "./model.js";""" +
                """export function mapped(xs:readonly number[]):string[]{return Array.from(xs,(n:number,i:number):string=>"v"+(n+i));}""" +
                """export function tagged(pair:Pair):string[]{return Array.from(pair,(x:number|string,i:number):string=>"p"+i);}""" +
                """export function letters(s:string):string[]{return Array.from(s,(c:string,i:number):string=>c+"#"+i);}""" +
                """export function withContext(xs:readonly number[],ctx:{bias:number}):number[]{return Array.from(xs,(n:number,i:number):number=>n+i+ctx.bias,ctx);}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {values,pair,text,ctx} from "./model.js";""" +
                """export {mapped,tagged,letters,withContext} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {values,pair,text,ctx,mapped,tagged,letters,withContext} from "./bridge.js";""" +
                """export const names:string[]=mapped(values);export const pairs:string[]=tagged(pair);""" +
                """export const lettersOut:string[]=letters(text);export const numeric:number[]=withContext(values,ctx);""" +
                """numeric.push(20);export const source=values;""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
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
                """import {names,pairs,lettersOut,numeric,source} from './entry.js';console.log(JSON.stringify({names,pairs,lettersOut,numeric,source}));""");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Array.from mapping execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal(
                """{"names":["v2","v5","v8"],"pairs":["p0","p1"],"lettersOut":["A#0","🚀#1"],"numeric":[5,8,11,20],"source":[2,4,6]}""",
                process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleMappingBlocksAllJavaScriptOutput()
    {
        const string root="/virtual/array-from-mapper-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]=
            """export const values:readonly number[]=[2,4];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";export const wrong:string[]=Array.from(values,(n:number):number=>n+1);""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
