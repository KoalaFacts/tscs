using System.Diagnostics;
using Tscs;
using Xunit;

namespace Tscs.Tests;

// Bounded parenthesized JS sequence expressions, not full TS7 acceptance.
public sealed class ParenthesizedCommaSequenceTests
{
    [Theory]
    [InlineData("function mark():number{return 20;}let result:number=(mark(),42);")]
    [InlineData("function mark():string{return \"x\";}let result:boolean=(mark(),true);")]
    [InlineData("function mark():number{return 2;}let result:string=(mark(),\"ready\");")]
    [InlineData("function mark():number{return 2;}function last():number{return 42;}let result:number=(mark(),last());")]
    [InlineData("function mark():number{return 2;}let result:number=(mark(),mark(),42);")]
    [InlineData("function mark():number{return 2;}let result:number=((mark(),mark()),42);")]
    [InlineData("function mark():number{return 2;}function use(n:number):number{return n;}let result:number=use((mark(),42));")]
    [InlineData("function mark():number{return 2;}function choose():number{return (mark(),42);}let result:number=choose();")]
    [InlineData("function mark():number{return 2;}let result:number=true?(mark(),42):0;")]
    [InlineData("function mark():number{return 2;}let result:boolean=(mark(),42)>0;")]
    [InlineData("function mark():number{return 0;}let result:42=(mark(),42);")]
    [InlineData("function mark():number{return 0;}let result:{value:42}=(mark(),{value:42});")]
    [InlineData("function mark():number{return 0;}let result:[number,string]=(mark(),[21,\"ready\"]);")]
    public void EffectfulParenthesizedSequencesCheckBothSides(string source)=>
        Assert.Empty(Compilation.Check("comma-sequence.ts",source));

    [Theory]
    [InlineData("let result:number=(21,42);","TS2695")]
    [InlineData("let value:number=1;let result:number=(value,42);","TS2695")]
    [InlineData("function mark():number{return 21;}let result:string=(mark(),42);","TS2322")]
    [InlineData("function mark():number{return 21;}let result:number=(mark(),missing);","TS2304")]
    [InlineData("function mark():number{return 21;}let result:number=(mark(),{value:1}.missing);","TS2339")]
    [InlineData("function mark(x:number):number{return x;}let result:number=(mark(\"bad\"),42);","TS2345")]
    [InlineData("function mark():number{return 21;}let result:number=(mark(),);","TSCS1001")]
    [InlineData("function mark():number{return 0;}let result:{value:number}=(mark(),{value:42,extra:1});","TS2353")]
    [InlineData("function mark():number{return 0;}function use(o:{value:number}):number{return o.value;}let result:number=use((mark(),{value:42,extra:1}));","TS2353")]
    public void InvalidOperandsAndUnusedPureLeftSideRemainErrors(string source,string code)=>
        Assert.Contains(Compilation.Check("comma-sequence-negative.ts",source),
            diagnostic=>diagnostic.Code==code);

    private sealed class Host : ICompilerHost
    {
        public StringComparer FileNameComparer=>StringComparer.Ordinal;
        public Dictionary<string,string> Files { get; }=new(StringComparer.Ordinal);
        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string ReadFile(string path)=>Files[path];
    }

    [Fact]
    public void OrderedEffectsAndFinalTypeExecuteAcrossFourModules()
    {
        var root=Directory.CreateTempSubdirectory("tscs-comma-sequence-").FullName;
        try
        {
            var host=new Host();
            host.Files[Path.Combine(root,"model.ts")]=
                "export const state:{count:number}={count:0};";
            host.Files[Path.Combine(root,"logic.ts")]=
                "import {state} from \"./model.js\";"+
                "export function mark(amount:number):number {state.count=state.count+amount;return state.count;}";
            host.Files[Path.Combine(root,"bridge.ts")]=
                "export {state} from \"./model.js\";export {mark} from \"./logic.js\";";
            host.Files[Path.Combine(root,"entry.ts")]=
                "import {state,mark} from \"./bridge.js\";"+
                "export const result:number=(mark(20),mark(22));"+
                "export const last:number=state.count;";
            var program=CompilerProgram.Load(host,[Path.Combine(root,"entry.ts")],
                new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
            Assert.Empty(program.Analyze().Diagnostics);
            var emitted=program.EmitJavaScript();
            Assert.Equal(4,emitted.Count);
            foreach(var file in emitted)
                File.WriteAllText(Path.ChangeExtension(file.SourceFile,".js"),file.Text);
            File.WriteAllText(Path.Combine(root,"package.json"),"{\"type\":\"module\"}");
            var info=new ProcessStartInfo("node") {
                WorkingDirectory=root,UseShellExecute=false,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            info.ArgumentList.Add("--input-type=module");
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add("import {result,last} from './entry.js';console.log(JSON.stringify([result,last]));");
            using var process=Process.Start(info)!;
            if(!process.WaitForExit(10000))
            {
                process.Kill(entireProcessTree:true);
                throw new TimeoutException("Comma sequence runtime exceeded ten seconds.");
            }
            Assert.Equal(0,process.ExitCode);
            Assert.Equal("",process.StandardError.ReadToEnd());
            Assert.Equal("[42,42]",process.StandardOutput.ReadToEnd().Trim());
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void InvalidCrossModuleSequencePreventsOutput()
    {
        const string root="/virtual/comma-sequence-invalid";
        var host=new Host();
        host.Files[root+"/model.ts"]="export function mark():number{return 21;}";
        host.Files[root+"/entry.ts"]=
            "import {mark} from \"./model.js\";"+
            "export const result:number=(mark(),\"wrong\");";
        var program=CompilerProgram.Load(host,[root+"/entry.ts"],
            new CompilerOptions {NoLib=true,Target=CompilerTarget.Es2015,Module=CompilerModule.Es2015});
        Assert.Contains(program.Analyze().Diagnostics,diagnostic=>diagnostic.Code=="TS2322");
        Assert.Throws<InvalidOperationException>(()=>program.EmitJavaScript());
    }
}
