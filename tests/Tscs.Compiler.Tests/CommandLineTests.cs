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
    [Theory]
    [InlineData("\u2028",0)]
    [InlineData("\u2029",0)]
    [InlineData("\r",2)]
    [InlineData("\n",2)]
    public void RawStringLineCharactersKeepCorrectExitStatus(string character,int expected)
    {
        var r=Run("--noEmit","--noLib",Write("a.ts","let s: string = \"a"+character+"z\";"),Lib());
        Assert.Equal(expected,r.Code);
        if(expected==0)Assert.Equal("",r.Out);else Assert.Contains("TS1002",r.Out);
    }
    [Theory]
    [InlineData("\\n",0)]
    [InlineData("\\uZZZZ",2)]
    [InlineData("\\u0061",0)]
    [InlineData("\\x61",0)]
    [InlineData("\\u{1F600}",0)]
    public void BoundedStringEscapeSupportHasHonestExitStatus(string escape,int code)
    {
        var result=Run("--noEmit","--noLib",Write("a.ts","let s: string = \""+escape+"\";"),Lib());
        Assert.Equal(code,result.Code);
        if(code==1)Assert.Contains("TSCS1001",result.Out);else if(code==2)Assert.Contains("TS1125",result.Out);else Assert.Equal("",result.Out);
    }
    [Theory]
    [InlineData("\\xG","TS1125",2,0)]
    [InlineData("\\u{110000}","TS1198",3,6)]
    [InlineData("\\u{61G}","TS1199",5,0)]
    public void MalformedStringEscapesHaveSyntaxExitAndUtf16JsonLocations(string escape,string code,int relativeStart,int length)
    {
        string source="let s = \"😀"+escape+"\";";
        var result=Run("--json","--noEmit","--noLib",Write("a.ts",source));
        using var d=System.Text.Json.JsonDocument.Parse(result.Out);var diagnostic=Assert.Single(d.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(2,result.Code);Assert.Equal(2,d.RootElement.GetProperty("exitCode").GetInt32());Assert.Equal(code,diagnostic.GetProperty("code").GetString());
        Assert.Equal(source.IndexOf(escape,StringComparison.Ordinal)+relativeStart,diagnostic.GetProperty("start").GetInt32());Assert.Equal(length,diagnostic.GetProperty("length").GetInt32());Assert.DoesNotContain("TS2318",result.Out);
    }
    [Theory]
    [InlineData("\\0",0,"")]
    [InlineData("\\q",0,"")]
    [InlineData("\\177",2,"TS1487")]
    [InlineData("\\9",2,"TS1488")]
    public void NumericAndIdentityStringEscapesKeepCorrectExitStatus(string escape,int exit,string code)
    {
        var result=Run("--json","--noEmit","--noLib",Write("a.ts","let s = \"😀"+escape+"\";"),Lib());
        Assert.Equal(exit,result.Code);using var d=System.Text.Json.JsonDocument.Parse(result.Out);var diagnostics=d.RootElement.GetProperty("diagnostics").EnumerateArray().ToArray();
        if(exit==0)Assert.Empty(diagnostics);else {var error=Assert.Single(diagnostics);Assert.Equal(code,error.GetProperty("code").GetString());Assert.Equal(11,error.GetProperty("start").GetInt32());Assert.Equal(escape.Length,error.GetProperty("length").GetInt32());}
    }
    [Fact]
    public void NullConfigBooleansAllowExplicitCommandLineOverrides()
    {
        Write("a.ts","let n = 1;");Lib();
        var project=Write("tsconfig.json","{\"files\":[\"a.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":null,\"noLib\":null}}");
        Assert.Equal(0,Run("-p",project,"--noEmit","--noLib").Code);
    }
    [Theory]
    [InlineData("noEmit","0")]
    [InlineData("noEmit","\"true\"")]
    [InlineData("noEmit","{}")]
    [InlineData("noEmit","[]")]
    [InlineData("noLib","-0")]
    [InlineData("noLib","\"false\"")]
    [InlineData("noLib","{\"x\":false}")]
    [InlineData("noLib","[null]")]
    public void InvalidConfigBooleanReportsPinnedOptionDiagnostic(string name,string value)
    {
        string source="/*😀*/ {\"compilerOptions\": {\""+name+"\": /*value*/ "+value+"}}";
        var project=Write("tsconfig.json",source);var result=Run("--json","-p",project,"--noEmit","--noLib");
        Assert.Equal(2,result.Code);using var document=System.Text.Json.JsonDocument.Parse(result.Out);
        var diagnostic=Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("TS5024",diagnostic.GetProperty("code").GetString());Assert.Equal("tsconfig.json",diagnostic.GetProperty("file").GetString());
        Assert.Equal(source.IndexOf(value,StringComparison.Ordinal),diagnostic.GetProperty("start").GetInt32());
        Assert.Equal(value.Length,diagnostic.GetProperty("length").GetInt32());
        Assert.Equal("Compiler option '"+name+"' requires a value of type boolean.",diagnostic.GetProperty("message").GetString());
    }
    [Theory]
    [InlineData("")]
    [InlineData("--noEmit")]
    [InlineData("--noLib")]
    [InlineData("--noEmit false --noLib")]
    [InlineData("--noEmit --noLib false")]
    public void NullConfigBooleansKeepUnspecifiedBootstrapRequirements(string flags)
    {
        Write("a.ts","let n = 1;");Lib();
        var project=Write("tsconfig.json","{\"files\":[\"a.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":null,\"noLib\":null}}");
        var result=Run(new[]{"-p",project}.Concat(flags.Split(' ',StringSplitOptions.RemoveEmptyEntries)).ToArray());
        Assert.Equal(1,result.Code);Assert.Contains("bootstrap requires",result.Out);
    }
    [Theory]
    [InlineData("\"bogus\"")]
    [InlineData("0")]
    public void InvalidConfigTargetMatchesActualPinnedEnumDiagnostic(string literal)
    {
        using var golden=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/json-enum-options.json")));
        var row=golden.RootElement.GetProperty("vectors").EnumerateArray().Single(r=>r.GetProperty("option").GetProperty("name").GetString()=="target"&&r.GetProperty("literal").GetString()==literal&&r.GetProperty("located").GetBoolean());
        var result=Run("--json","-p",Write("tsconfig.json",row.GetProperty("source").GetString()!));Assert.Equal(2,result.Code);
        using var output=System.Text.Json.JsonDocument.Parse(result.Out);var actual=Assert.Single(output.RootElement.GetProperty("diagnostics").EnumerateArray());var expected=Assert.Single(row.GetProperty("diagnostics").EnumerateArray());
        foreach(var key in new[]{"code","message","start","length"})Assert.Equal(expected.GetProperty(key).ToString(),actual.GetProperty(key).ToString());
    }
    [Fact]
    public void ProjectUsesProductJsonStringDecoding()
    {
        Write("a.ts","let s = 1;");Lib();
        var project=Write("tsconfig.json","{\"files\":[\"\\a.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.Equal(0,Run("-p",project).Code);
    }
    [Fact]
    public void ProjectSingleQuotesReportJsonSyntaxDiagnostic()
    {
        var result=Run("--json","-p",Write("tsconfig.json","{'files':[]}"));
        Assert.Equal(2,result.Code);Assert.Contains("TS1327",result.Out);
    }
    [Fact]
    public void UnportedJsonIdentifierClassificationHasUnsupportedExit()
    {
        var result=Run("--json","-p",Write("tsconfig.json","{ⸯ:true}"));Assert.Equal(1,result.Code);Assert.Contains("TSCS1001",result.Out);
    }
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
    [Fact] public void UnsupportedPrefixUpdateHasNoSuccessExit()=>
        Assert.Equal(1,Run("--noEmit","--noLib",Write("a.ts","let x = 1; ++x;"),Lib()).Code);
    [Fact] public void ImplementedPostfixUpdateHasSuccessExit()=>
        Assert.Equal(0,Run("--noEmit","--noLib",Write("a.ts","let x = 1; x++;"),Lib()).Code);
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
    [Fact]
    public void SourceBackedConfigMissingCommaReportsPinnedSyntaxSpanAndExit()
    {
        using var golden=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/source-config-ast-missing-comma.json")));
        var text=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(golden.RootElement.GetProperty("expected").GetProperty("selection").GetProperty("configFile").GetProperty("content").GetString()!));
        var result=Run("--json","-p",Write("tsconfig.json",text));Assert.Equal(2,result.Code);
        using var json=System.Text.Json.JsonDocument.Parse(result.Out);var diagnostic=Assert.Single(json.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("TS1005",diagnostic.GetProperty("code").GetString());Assert.Equal(164,diagnostic.GetProperty("start").GetInt32());Assert.Equal(9,diagnostic.GetProperty("length").GetInt32());Assert.Equal("',' expected.",diagnostic.GetProperty("message").GetString());
    }
    [Theory]
    [InlineData("""{"a":1 "b":"\xG"}""","TS1005,TS1125")]
    [InlineData("""{"a":1 "\xG":"\uZZZZ"}""","TS1125,TS1005,TS1125")]
    public void ProjectMixedSyntaxDiagnosticsKeepScannerParserEventOrder(string source,string codes)
    {
        var result=Run("--json","-p",Write("tsconfig.json",source));Assert.Equal(2,result.Code);
        using var json=System.Text.Json.JsonDocument.Parse(result.Out);
        Assert.Equal(codes.Split(','),json.RootElement.GetProperty("diagnostics").EnumerateArray().Select(d=>d.GetProperty("code").GetString()));
    }
}
