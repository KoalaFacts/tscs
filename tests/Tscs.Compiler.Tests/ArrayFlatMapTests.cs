using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Source-backed compiler checks, separate from original pinned TS7 acceptance.
public sealed class ArrayFlatMapTests
{
    [Theory]
    [InlineData("let xs:number[]=[2,3];let out:number[]=xs.flatMap((n:number):number[]=>[n,n+1]);")]
    [InlineData("let xs:readonly number[]=[2,3];let out:number[]=xs.flatMap((n:number):number[]=>[n,n+1]);out[0]=42;")]
    [InlineData("let xs:ReadonlyArray<string>=[\"a\",\"b\"];let out:string[]=xs.flatMap((s:string):string[]=>[s,s+\"!\"]);")]
    [InlineData("let xs:number[]=[2,3];let out:number[]=xs.flatMap((n:number):number=>n*2);")]
    [InlineData("let xs:number[]=[2,3];let out:number[][]=xs.flatMap((n:number):number[][]=>[[n,n+1]]);")]
    [InlineData("let xs:number[]=[2,3];let out:string[]=xs.flatMap((n:number):string=>\"v\"+n);")]
    [InlineData("let xs:number[]=[2,3];let out:number[][]=xs.map((n:number):number[]=>[n,n+1]);")]
    [InlineData("let xs:number[]=[2,3];let out:number[]=xs.map((n:number):number[]=>[n]).flatMap((ns:number[]):number[]=>ns);")]
    [InlineData("function twice(n:number):number[]{return [n,n];}let xs:readonly number[]=[2,3];let out:number[]=xs.flatMap(twice);")]
    public void ExplicitCallbackResultsFlattenOneArrayLevel(string source) =>
        Assert.Empty(Compilation.Check("flat-map.ts",source));

    [Theory]
    [InlineData("let xs:number[]=[1];xs.flatMap();","TS2554")]
    [InlineData("let xs:number[]=[1];xs.flatMap(42);","TS2345")]
    [InlineData("let xs:number[]=[1];xs.flatMap((s:string):string[]=>[s]);","TS2345")]
    [InlineData("let xs:number[]=[1];let wrong:string[]=xs.flatMap((n:number):number[]=>[n]);","TS2322")]
    [InlineData("let xs:number[]=[1];let wrong:number[]=xs.flatMap((n:number):string[]=>[\"x\"]);","TS2322")]
    [InlineData("let xs:number[]=[1];let wrong:number[]=xs.flatMap((n:number):number[][]=>[[n]]);","TS2322")]
    [InlineData("let xs:readonly number[]=[1];xs.flatMap((s:string):number[]=>[1]);","TS2345")]
    [InlineData("let xs:number[]=[1];let wrong:string[]=xs.map((n:number):number[]=>[n]);","TS2322")]
    public void InvalidFlatMapCallbacksAndResultTypesFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("flat-map-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; } = new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string ReadFile(string path) => Files[path];
    }

    [Fact]
    public void FourLinkedModulesExecuteNativeFlatMapAndKeepSourceUnmodified()
    {
        var root=Directory.CreateTempSubdirectory("tscs-flat-map-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                "export type Numbers=ReadonlyArray<number>;export const values:Numbers=[2,3];";
            host.Files[Path.Combine(root,"logic.ts")]=
                "import type {Numbers} from \"./model.js\";"+
                "export function spread(xs:Numbers):number[]{return xs.flatMap((n:number):number[]=>[n,n+1]);}"+
                "export function double(xs:Numbers):number[]{return xs.flatMap((n:number):number=>n*2);}";
            host.Files[Path.Combine(root,"bridge.ts")]=
                "export {values} from \"./model.js\";export {spread,double} from \"./logic.js\";";
            host.Files[Path.Combine(root,"entry.ts")]=
                "import {values,spread,double} from \"./bridge.js\";"+
                "export const expanded:number[]=spread(values);"+
                "export const doubled:number[]=double(values);expanded[0]=42;"+
                "export const original:ReadonlyArray<number>=values;";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var outputs=program.EmitJavaScript();
            Assert.Equal(4,outputs.Count);
            Assert.Contains(outputs,js=>js.Text.Contains(".flatMap(",StringComparison.Ordinal));
            foreach(var output in outputs)
                File.WriteAllText(Path.ChangeExtension(output.SourceFile,".js"),output.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"{\"type\":\"module\"}");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                "import {expanded,doubled,original} from './entry.js';"+
                "console.log(expanded.join(',')+'|'+doubled.join(',')+'|'+original.join(','));");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Node flatMap execution exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("42,3,3,4|4,6|2,3",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleResultPreventsAnyJavascriptOutput()
    {
        const string root="/virtual/flat-map-negative";
        var host=new Host();
        host.Files[root+"/model.ts"]="export const values:readonly number[]=[2,3];";
        host.Files[root+"/entry.ts"]=
            "import {values} from \"./model.js\";"+
            "let wrong:string[]=values.flatMap((n:number):number[]=>[n]);";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
