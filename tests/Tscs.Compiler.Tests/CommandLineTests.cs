using Xunit;
using Tscs;
namespace Tscs.Tests;
public class CommandLineTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-test-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);
    private (int Code,string Out) Run(params string[] args){using var writer=new StringWriter();var code=CommandLine.Run(args,writer,writer);return(code,writer.ToString());}
    private string Write(string name,string text){var path=Path.Combine(directory,name);File.WriteAllText(path,text);return path;}
    private string Lib()=>Write("minimal.d.ts","interface Array<T> {} interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
    [Fact] public void MissingInputFails()=>Assert.NotEqual(0,Run().Code);
    [Fact] public void UnknownOptionFails()=>Assert.NotEqual(0,Run("--imaginary").Code);
    [Fact] public void MissingFileFails()=>Assert.NotEqual(0,Run("--noEmit","--noLib",Path.Combine(directory,"missing.ts")).Code);
    [Fact] public void ValidSourceSucceeds()=>Assert.Equal(0,Run("--noEmit","--noLib",Write("a.ts","let n: number = 1;"),Lib()).Code);
    [Fact] public void ActualTypeErrorHasUpstreamExitStatus()
    {
        var r=Run("--noEmit","--noLib",Write("a.ts","let n: number = \"x\";"),Lib());
        Assert.Multiple(
            ()=>Assert.Equal(2,r.Code),
            ()=>Assert.Contains("(1,5): error TS2322: Type 'string' is not assignable to type 'number'.",r.Out));
    }
    [Fact] public void ProjectPathsAreRelativeToConfig()
    {
        Write("a.ts","let n: number = 1;");Lib();
        var p=Write("tsconfig.json","{\"files\":[\"a.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.Equal(0,Run("-p",p).Code);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"files\":[3]}")]
    [InlineData("{\"files\":[],\"extends\":\"./base.json\"}")]
    [InlineData("{\"files\":[],\"compilerOptions\":{\"strict\":true}}")]
    public void InvalidOrUnsupportedConfigFails(string json)=>Assert.NotEqual(0,Run("-p",Write("tsconfig.json",json)).Code);
    [Fact] public void UnsupportedCodeHasNoSuccessExit()=>Assert.Equal(1,Run("--noEmit","--noLib",Write("a.ts","let x = 1; x++;"),Lib()).Code);
    [Theory]
    [InlineData("globalThis;", "TSCS1001", 1)]
    [InlineData("{ let globalThis = 1; globalThis; globalThis = 2; }", "", 0)]
    [InlineData("{ let globalThis = 1; } globalThis;", "TSCS1001", 1)]
    [InlineData("globalThis = 1;", "TSCS1001", 1)]
    [InlineData("let globalThis = 1;", "TS2397", 2)]
    [InlineData("missing;", "TS2304", 2)]
    [InlineData("globalthis;", "TS2304", 2)]
    public void GlobalThisAndMissingNamesHaveCorrectDiagnosticsAndExitStatus(string source,string code,int exitCode)
    {
        var args=new[]{"--noEmit","--noLib",Write("a.ts",source),Lib()};
        var text=Run(args);
        var json=Run(args.Append("--json").ToArray());
        using var document=System.Text.Json.JsonDocument.Parse(json.Out);
        var diagnostics=document.RootElement.GetProperty("diagnostics").EnumerateArray().ToArray();
        Assert.Multiple(
            ()=>Assert.Equal(exitCode,text.Code),
            ()=>Assert.Equal(exitCode,json.Code),
            ()=>Assert.Equal(exitCode,document.RootElement.GetProperty("exitCode").GetInt32()),
            ()=>Assert.Equal(code.Length==0?Array.Empty<string>():new[]{code},diagnostics.Select(d=>d.GetProperty("code").GetString())),
            ()=>{if(code.Length==0)Assert.Empty(text.Out);else Assert.Contains($": error {code}:",text.Out);},
            ()=>{if(code=="TSCS1001")Assert.Contains("The built-in globalThis type is not supported yet.",text.Out);},
            ()=>{if(code=="TSCS1001")Assert.DoesNotContain("TS2304",text.Out);});
    }
    [Fact] public void MissingGlobalsAreReported()=>Assert.Contains("TS2318",Run("--noEmit","--noLib",Write("a.ts","let x = 1;")).Out);
    [Fact] public void UnsupportedAmbientMembersAreRejected()=>Assert.Equal(1,Run("--noEmit","--noLib",Write("a.ts","let x = 1;"),Write("bad.d.ts","interface Object { x: number } ")).Code);
    [Fact] public void MissingGlobalsPrecedeCheckerGrammarErrors()
    {
        var r=Run("--noEmit","--noLib",Write("a.ts","const n: number;"));
        Assert.Multiple(()=>Assert.Contains("TS2318",r.Out),()=>Assert.DoesNotContain("TS1155",r.Out));
    }
}
