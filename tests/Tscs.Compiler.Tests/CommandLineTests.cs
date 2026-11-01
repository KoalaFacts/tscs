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
    [Fact] public void ImplementedPrefixUpdateHasSuccessExit()=>
        Assert.Equal(0,Run("--noEmit","--noLib",Write("a.ts","let x = 1; ++x;"),Lib()).Code);
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
    [Fact]
    public void FiniteConcatAmbientRequiresAnExactNonDuplicatedSignature()
    {
        var source=Write("concat-ambient.ts","let xs:readonly number[]=[1,2];let ys:number[]=xs.concat([3],4);");
        const string profile=
            "interface Array<T> { length: number; [index: number]: T; concat(...items: (T | ReadonlyArray<T>)[]): T[]; }\n" +
            "interface ReadonlyArray<T> { readonly length: number; readonly [index: number]: T; concat(...items: (T | ReadonlyArray<T>)[]): T[]; }\n" +
            "interface Boolean {} interface Function {} interface IArguments {} interface Number {} " +
            "interface Object {} interface RegExp {} interface String {} interface CallableFunction {} interface NewableFunction {}";
        var ambient=Write("concat-ambient.d.ts",profile);
        var ok=Run("--noEmit","--noLib","--target","es2015",source,ambient);
        Assert.Equal(0,ok.Code);

        // An ambient profile may not invent broader array operations, alter the
        // return type, or quietly merge duplicate native declarations.
        foreach(var invalid in new[]{
            profile.Replace("concat(...items: (T | ReadonlyArray<T>)[]): T[];",
                "concat(...items: (T | ReadonlyArray<T>)[]): any[];",StringComparison.Ordinal),
            profile.Replace("concat(...items: (T | ReadonlyArray<T>)[]): T[];",
                "concat(...items: (T | ReadonlyArray<T>)[]): T[]; concat(...items: (T | ReadonlyArray<T>)[]): T[];",StringComparison.Ordinal),
            profile.Replace("concat(...items: (T | ReadonlyArray<T>)[]): T[];",
                "concat(...items: any[]): T[];",StringComparison.Ordinal)
        })
        {
            File.WriteAllText(ambient,invalid);
            var rejected=Run("--noEmit","--noLib","--target","es2015",source,ambient);
            Assert.Equal(1,rejected.Code);
            Assert.Contains("TSCS1002",rejected.Out);
        }
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

    // Pinned original TS7 fixture: tsc/testdata/tests/cases/compiler/emptyFile.ts
    // Source SHA-256 a149b5cff5898a63f501618af2c01a132895281e03663831e76f6deb9f3b5bc7.
    // Original configuration target configuration:c5f81e2689b2db32a694b7b3de45098f5d58941800bfe0683e2f68000e24c110
    // Baseline: tsc/testdata/baselines/reference/compiler/emptyFile.js at the locked upstream commit.
    [Fact]
    public void OriginalEmptyFileProfileMatchesPinnedEs2015EmitBaseline()
    {
        const string fixture="// @target: es2015\n";
        Assert.Equal("a149b5cff5898a63f501618af2c01a132895281e03663831e76f6deb9f3b5bc7",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fixture))).ToLowerInvariant());
        var input=Write("emptyFile.ts",fixture);
        var result=Run("--target","es2015",input); // EXACT original profile: no added --noEmit or --noLib
        Assert.Equal(0,result.Code);
        Assert.Equal("",result.Out);
        var js=Path.ChangeExtension(input,".js");
        Assert.True(File.Exists(js),"Original profile must emit JavaScript; diagnostics alone are insufficient.");
        Assert.Equal("\"use strict\";\r\n",File.ReadAllText(js));
    }

    [Theory]
    [InlineData("no-target")]
    [InlineData("noEmit-only")]
    [InlineData("noLib-only")]
    public void OriginalEmptyEmitSliceDoesNotUnlockOtherFlagCombinations(string mode)
    {
        var source=Write("emptyFile.ts","// @target: es2015\n");
        var args=mode switch
        {
            "no-target" => Array.Empty<string>(),
            "noEmit-only" => new[]{"--target","es2015","--noEmit"},
            _ => new[]{"--target","es2015","--noLib"}
        };
        var result=Run(args.Append(source).ToArray());
        Assert.Equal(1,result.Code);
        Assert.Contains("bootstrap requires",result.Out);
        Assert.False(File.Exists(Path.ChangeExtension(source,".js")));
    }

    [Fact]
    public void OriginalEmptyEmitNeverOverwritesExistingJavaScript()
    {
        var source=Write("emptyFile.ts","// @target: es2015\n");
        var emitted=Write("emptyFile.js","existing user content");
        var result=Run("--target","es2015",source);
        Assert.Equal(1,result.Code);
        Assert.Contains("TSCS1002",result.Out);
        Assert.Equal("existing user content",File.ReadAllText(emitted));
    }
    [Fact]
    public void MultipleNoEmitScriptsResolveGlobalsAndProjectFiles()
    {
        var a=Write("a.ts","let first: number = second;");
        var b=Write("b.ts","let second: number = 2;");
        var lib=Lib();
        Assert.Equal(0,Run("--noEmit","--noLib",a,b,lib).Code);
        Assert.Equal(0,Run("--noEmit","--noLib",b,a,lib).Code);
        var project=Write("tsconfig.json", "{\"files\":[\"a.ts\",\"b.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.Equal(0,Run("-p",project).Code);
    }
    [Fact]
    public void MultiFileJsonDiagnosticsDisambiguateIdenticalBasenames()
    {
        var dirA=Directory.CreateDirectory(Path.Combine(directory,"a")).FullName;
        var dirB=Directory.CreateDirectory(Path.Combine(directory,"b")).FullName;
        var a=Path.Combine(dirA,"index.ts");
        var b=Path.Combine(dirB,"index.ts");
        File.WriteAllText(a,"let a = ;");
        File.WriteAllText(b,"let b = ;");

        var result=Run("--json","--noEmit","--noLib",a,b);
        Assert.Equal(2,result.Code);
        using(var json=System.Text.Json.JsonDocument.Parse(result.Out))
        {
            var diagnostics=json.RootElement.GetProperty("diagnostics").EnumerateArray().ToArray();
            Assert.Equal(2,diagnostics.Length);
            Assert.All(diagnostics,d=>Assert.Equal("TS1109",d.GetProperty("code").GetString()));
            var identities=diagnostics.Select(d=>d.GetProperty("file").GetString()).ToArray();
            Assert.Equal(2,identities.Distinct(StringComparer.Ordinal).Count());
            Assert.Contains(Path.GetRelativePath(Environment.CurrentDirectory,a).Replace('\\','/'),identities);
            Assert.Contains(Path.GetRelativePath(Environment.CurrentDirectory,b).Replace('\\','/'),identities);
        }

        var single=Run("--json","--noEmit","--noLib",a);
        using var one=System.Text.Json.JsonDocument.Parse(single.Out);
        Assert.Equal(2,single.Code);
        Assert.Equal("index.ts",
            Assert.Single(one.RootElement.GetProperty("diagnostics").EnumerateArray())
                .GetProperty("file").GetString());
    }

    [Fact]
    public void UnixLiteralBackslashesRemainDistinctInJsonAndTextDiagnostics()
    {
        if(OperatingSystem.IsWindows())return; // A backslash is a real separator there.
        var unusual=Directory.CreateDirectory(Path.Combine(directory,"a\\b")).FullName;
        var ordinary=Directory.CreateDirectory(Path.Combine(directory,"a","b")).FullName;
        var a=Path.Combine(unusual,"index.ts");
        var b=Path.Combine(ordinary,"index.ts");
        File.WriteAllText(a,"let a = ;");
        File.WriteAllText(b,"let b = ;");

        var json=Run("--json","--noEmit","--noLib",a,b);
        Assert.Equal(2,json.Code);
        using(var result=System.Text.Json.JsonDocument.Parse(json.Out))
        {
            var errors=result.RootElement.GetProperty("diagnostics").EnumerateArray().ToArray();
            Assert.Equal(2,errors.Length);
            Assert.Equal(2,errors.Select(d=>d.GetProperty("file").GetString()).Distinct().Count());
            Assert.Contains(Path.GetRelativePath(Environment.CurrentDirectory,a),
                errors.Select(d=>d.GetProperty("file").GetString()));
            Assert.Contains(Path.GetRelativePath(Environment.CurrentDirectory,b),
                errors.Select(d=>d.GetProperty("file").GetString()));
        }

        var text=Run("--noEmit","--noLib",a,b);
        Assert.Equal(2,text.Code);
        Assert.Contains(Path.GetRelativePath(Environment.CurrentDirectory,a)+"(",text.Out);
        Assert.Contains(Path.GetRelativePath(Environment.CurrentDirectory,b)+"(",text.Out);
    }

    [Fact]
    public void WindowsModuleDiagnosticUsesLoadedCaseAndSourceCoordinates()
    {
        if(!OperatingSystem.IsWindows())return;
        var a=Write("A.ts","export { value } from \"./b\";");
        var b=Write("B.ts","\n\nexport { value } from \"./b\";");
        var result=Run("--noEmit","--noLib",a,b);
        Assert.Equal(1,result.Code);
        Assert.Contains("B.ts(3,10): error TSCS1001:",result.Out);
        Assert.DoesNotContain("b.ts(1,12)",result.Out);
    }

    [Fact]
    public void CrossFileTypeErrorsRenderTheCorrectFile()
    {
        var a=Write("a.ts","let first: string = second;");
        var b=Write("b.ts","let second: number = 2;");
        var r=Run("--noEmit","--noLib",a,b,Lib());
        Assert.Equal(2,r.Code);
        Assert.Contains("a.ts(1,5): error TS2322:",r.Out);
    }
    [Fact]
    public void CrossFileDuplicatesAndComplexInferenceNeverSucceedSilently()
    {
        var a=Write("a.ts","let repeated: number = 1;");
        var b=Write("b.ts","let repeated: number = 2;");
        var duplicate=Run("--noEmit","--noLib",a,b,Lib());
        Assert.Equal(1,duplicate.Code);
        Assert.Contains("TSCS1001",duplicate.Out);
        Assert.Contains("b.ts(",duplicate.Out);

        File.WriteAllText(a,"let result: boolean = inferred;");
        File.WriteAllText(b,"let inferred = 1 + 2;");
        var unknown=Run("--noEmit","--noLib",a,b,Path.Combine(directory,"minimal.d.ts"));
        Assert.Equal(1,unknown.Code);
        Assert.Contains("Cross-file inference",unknown.Out);
    }
    [Fact]
    public void ValidatedIterableAmbientAllowsCheckedArraySpread()
    {
        const string profile="interface SymbolConstructor { readonly iterator: unique symbol; }\n"+
            "declare const Symbol: SymbolConstructor;\n"+
            "interface IteratorResult<T> { done?: boolean; value: T; }\n"+
            "interface Iterator<T, TReturn = any, TNext = any> { next(): IteratorResult<T>; }\n"+
            "interface Array<T> { length: number; [index: number]: T; [Symbol.iterator](): Iterator<T>; }\n"+
            "interface Boolean {} interface Function {} interface IArguments {} interface Number {} "+
            "interface Object {} interface RegExp {} interface String {} "+
            "interface CallableFunction {} interface NewableFunction {}";
        var source=Write("spread.ts","function f(...items:number[]):number{return items.length;}"+
            "let xs:number[]=[20,21];let n:number=f(...xs);");
        var ambient=Write("iterable.d.ts",profile);
        var ok=Run("--noEmit","--noLib","--target","es2015",source,ambient);
        Assert.Equal(0,ok.Code);
        Assert.Equal("",ok.Out);
        File.WriteAllText(ambient,profile.Replace("declare const Symbol: SymbolConstructor;","",StringComparison.Ordinal));
        var missing=Run("--noEmit","--noLib","--target","es2015",source,ambient);
        Assert.Equal(1,missing.Code);
        Assert.Contains("TSCS1002",missing.Out);
        File.WriteAllText(ambient,profile.Replace("next(): IteratorResult<T>;","next(): any;",StringComparison.Ordinal));
        var fake=Run("--noEmit","--noLib","--target","es2015",source,ambient);
        Assert.Equal(1,fake.Code);
        Assert.Contains("TSCS1002",fake.Out);
    }
    [Fact]
    public void CrossFileRootGlobalThisStillRequiresIntrinsicHandling()
    {
        var a=Write("a.ts","let globalThis = 1;");
        var b=Write("b.ts","let other: number = 2;");
        var r=Run("--noEmit","--noLib",a,b,Lib());
        Assert.Equal(2,r.Code);
        Assert.Contains("TS2397",r.Out);
    }

}
