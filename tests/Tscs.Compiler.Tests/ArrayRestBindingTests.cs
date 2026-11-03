using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ArrayRestBindingTests
{
    [Theory]
    [InlineData("const xs:number[]=[20,21,22];const [first,...tail]=xs;let n:number=first+tail[0];")]
    [InlineData("const xs:number[]=[20,21,22];const [,,...tail]=xs;let n:number=tail[0];")]
    [InlineData("const xs:readonly number[]=[20,22];let [...copy]=xs;copy[0]=42;")]
    [InlineData("const xs:number[][]=[[20],[22]];const [[first],...remaining]=xs;let n:number=first+remaining[0][0];")]
    [InlineData("function sum([head,...tail]:number[]):number{return head+tail[0];}let n:number=sum([20,22]);")]
    [InlineData("const f=([head,...tail]:number[]):number=>head+tail[0];let n:number=f([20,22]);")]
    [InlineData("const {values:[first,...tail]}={values:[20,22]};let n:number=first+tail[0];")]
    [InlineData("const xs:number[]=[21,22];const [...copy]=xs;let n:number=copy[1];")]
    [InlineData("const [head,...tail]:number[]=[21,22];let n:number=head+tail[0];")]
    public void NativeArrayRestBindingsUseExistingCheckedTypePaths(string source) =>
        Assert.Empty(Compilation.Check("array-rest.ts",source));

    [Theory]
    [InlineData("const [...tail, last]=[20,22];","TSCS1001")]
    [InlineData("const [...tail,]=[20,22];","TSCS1001")]
    [InlineData("const [...tail=[1]]=[20,22];","TSCS1001")]
    [InlineData("const [...[tail]]=[20,22];","TSCS1001")]
    [InlineData("const [...tail]=\"hi\";","TSCS1001")]
    [InlineData("const [...tail]={0:21};","TSCS1001")]
    [InlineData("let pair:[number,string]=[21,\"x\"];const [head,...tail]=pair;","TSCS1001")]
    [InlineData("const [head,...tail]=[21,22];let wrong:string=tail[0];","TS2322")]
    [InlineData("const [head,...tail]=[21,22];tail=[1];","TS2588")]
    public void UnsafeOrInvalidArrayRestBindingsFailClosed(string source,string code) =>
        Assert.Contains(Compilation.Check("array-rest-invalid.ts",source),
            diagnostic=>diagnostic.Code==code);

    [Fact]
    public void LinkedArrayRestBindingsEmitAndExecuteRealEs2015()
    {
        var root=Directory.CreateTempSubdirectory("tscs-array-rest-").FullName;
        try
        {
            var sources=new Dictionary<string,string>(StringComparer.Ordinal)
            {
                [Path.Combine(root,"values.ts")]="export const nums:number[]=[20,21,22];",
                [Path.Combine(root,"sum.ts")]="export function sum(a:number,b:number):number{return a+b;}",
                [Path.Combine(root,"bridge.ts")]="export {sum} from \"./sum.js\";",
                [Path.Combine(root,"entry.ts")]=
                    "import {nums} from \"./values.js\";import {sum} from \"./bridge.js\";"+
                    "const [first,...tail]=nums;"+
                    "export const result:number=sum(first,tail[1]);"
            };
            var program=CompilerProgram.Load(new SourceHost(sources),
                [Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var outputs=program.EmitJavaScript();
            Assert.Equal(4,outputs.Count);
            foreach(var output in outputs)
                File.WriteAllText(Path.ChangeExtension(output.SourceFile,".js"),output.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"{\"type\":\"module\"}");
            var info=new ProcessStartInfo("node")
            {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            // Dynamic import after overriding the source array's own slice
            // guards against accidentally lowering rest to nums.slice(...).
            info.ArgumentList.Add("import {nums} from './values.js';nums.slice=()=>{throw new Error('own slice invoked')};const {result}=await import('./entry.js');console.log(result)");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Node ES2015 array-rest execution exceeded 10 seconds.");
            }
            var stdout=process.StandardOutput.ReadToEnd();
            var stderr=process.StandardError.ReadToEnd();
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",stderr);
            Assert.Equal("42",stdout.Trim());
        }
        finally { Directory.Delete(root,true); }
    }

    [Fact]
    public void InvalidImportedRestTypesPreventAnyModuleEmissions()
    {
        var root=Path.Combine(Path.GetTempPath(),"tscs-array-rest-rejected");
        var sources=new Dictionary<string,string>(StringComparer.Ordinal)
        {
            [Path.Combine(root,"values.ts")]="export const nums:string[]=[\"bad\"];",
            [Path.Combine(root,"entry.ts")]=
                "import {nums} from \"./values.js\";const [...rest]=nums;let wrong:number=rest[0];"
        };
        var program=CompilerProgram.Load(new SourceHost(sources),
            [Path.Combine(root,"entry.ts")],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }

    private sealed class SourceHost(IReadOnlyDictionary<string,string> sources):ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string canonicalFileName)=>sources[canonicalFileName];
    }
}
