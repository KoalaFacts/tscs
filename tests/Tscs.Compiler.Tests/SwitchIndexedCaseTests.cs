using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Product-scoped compiler validation, not an original pinned TS7 PASS.
public sealed class SwitchIndexedCaseTests
{
    [Theory]
    [InlineData("""const values:number[]=[2,4];switch(2){case values[0]:{break;}}""")]
    [InlineData("""const values:[number,number]=[2,4];switch(4){case values[1]:{break;}}""")]
    [InlineData("""const values:{items:number[]}={items:[2,4]};switch(4){case values.items[1]:{break;}}""")]
    [InlineData("""const values:number[][]=[[2,3]];switch(3){case values[0][1]:{break;}}""")]
    [InlineData("""const values:{code:number}[]=[{code:2}];switch(2){case values[0].code:{break;}}""")]
    [InlineData("""function choose(i:number):number{const values:number[]=[2,4];switch(4){case values[i]:{return 1;}default:{return 0;}}}""")]
    [InlineData("""function choose(i:number):number{const values:number[]=[2,4];switch(4){case values[(i+1)]:{return 1;}default:{return 0;}}}""")]
    [InlineData("""const values:string[]=["red","blue"];let i:number=1;switch("blue"){case values[i]:{break;}}""")]
    [InlineData("""const code:string="ok";switch("o"){case code[0]:{break;}}""")]
    public void BoundedCheckedIndicesMayFormPrimitiveCases(string source) =>
        Assert.Empty(Compilation.Check("switch-index-case.ts",source));

    [Theory]
    [InlineData("""const values:number[]=[2,4];switch("bad"){case values[0]:{break;}}""","TS2678")]
    [InlineData("""const values:number[]=[2,4];switch(2){case values["0"]:{break;}}""","TSCS1001")]
    [InlineData("""const values:number[]=[2,4];let i:number=0;switch(2){case values[i++]:{break;}}""","TSCS1001")]
    [InlineData("""const values:number[]=[2,4];function next():number{return 0;}switch(2){case values[next()]:{break;}}""","TSCS1001")]
    [InlineData("""const values:number[]=[2,4];const key:{value:number}={value:0};switch(2){case values[key]:{break;}}""","TSCS1001")]
    [InlineData("""const values:{code:number}[]=[{code:2}];switch(2){case values[0].missing:{break;}}""","TS2339")]
    [InlineData("""const values:number[]=[2,4];switch(2){case values?.[0]:{break;}}""","TSCS1001")]
    [InlineData("""const values:{code:number}[]=[{code:2}];switch(2){case values[0].code++:{break;}}""","TSCS1001")]
    public void UnsafeOrIncompatibleIndicesStayRejected(string source,string code) =>
        Assert.Contains(Compilation.Check("switch-index-invalid.ts",source),d=>d.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer => StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void FourLinkedModulesExecuteIndexedCaseValues()
    {
        var root=Directory.CreateTempSubdirectory("tscs-switch-index-cases-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                """export const numbers:number[]=[2,4,6];export const records:{value:number}[]=[{value:3},{value:5}];""" +
                """export const grid:number[][]=[[7,8]];export const words:string[]=["ready","go"];""";
            host.Files[Path.Combine(root,"logic.ts")]=
                """import {numbers,records,grid,words} from "./model.js";""" +
                """export function choose(n:number):number{let offset:number=1;switch(n){""" +
                """case numbers[0]:{return 11;}case numbers[offset]:{return 22;}""" +
                """case records[0].value:{return 33;}case grid[0][1]:{return 44;}default:{return 0;}}}""" +
                """export function text(s:string):number{switch(s){case words[1]:{return 55;}default:{return 0;}}}""";
            host.Files[Path.Combine(root,"bridge.ts")]=
                """export {choose,text} from "./logic.js";""";
            host.Files[Path.Combine(root,"entry.ts")]=
                """import {choose,text} from "./bridge.js";""" +
                """export const answer:number=choose(2)*100000000+choose(4)*1000000+choose(3)*10000+choose(8)*100+text("go");""";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            Assert.Contains(emitted,file=>file.Text.Contains("case numbers[0]:",StringComparison.Ordinal));
            Assert.Contains(emitted,file=>file.Text.Contains("case records[0].value:",StringComparison.Ordinal));
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"""{"type":"module"}""");
            var start=new ProcessStartInfo("node")
            {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            start.ArgumentList.Add("--input-type=module");
            start.ArgumentList.Add("-e");
            start.ArgumentList.Add("""import {answer} from './entry.js';console.log(JSON.stringify(answer));""");
            using var process=Process.Start(start)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Indexed case module run exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("1122334455",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidImportedIndexCasePreventsAllJavaScriptOutput()
    {
        const string root="/virtual/switch-index-case-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="""export const values:number[]=[2,4];""";
        host.Files[root+"/entry.ts"]=
            """import {values} from "./model.js";switch("bad"){case values[0]:{break;}}""";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,d=>d.Code=="TS2678");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
