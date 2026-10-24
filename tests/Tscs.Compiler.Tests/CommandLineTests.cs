using NUnit.Framework;
using Tscs;
namespace Tscs.Tests;
public class CommandLineTests
{
    private string directory=null!;
    [SetUp] public void Setup()=>directory=Directory.CreateTempSubdirectory("tscs-test-").FullName;
    [TearDown] public void Cleanup()=>Directory.Delete(directory,true);
    private (int Code,string Out) Run(params string[] args){using var writer=new StringWriter();var code=CommandLine.Run(args,writer,writer);return(code,writer.ToString());}
    private string Write(string name,string text){var path=Path.Combine(directory,name);File.WriteAllText(path,text);return path;}
    private string Lib()=>Write("minimal.d.ts","interface Array<T> {} interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
    [Test] public void MissingInputFails()=>Assert.That(Run().Code,Is.Not.Zero);
    [Test] public void UnknownOptionFails()=>Assert.That(Run("--imaginary").Code,Is.Not.Zero);
    [Test] public void MissingFileFails()=>Assert.That(Run("--noEmit","--noLib",Path.Combine(directory,"missing.ts")).Code,Is.Not.Zero);
    [Test] public void ValidSourceSucceeds()=>Assert.That(Run("--noEmit","--noLib",Write("a.ts","let n: number = 1;"),Lib()).Code,Is.Zero);
    [Test] public void ActualTypeErrorHasUpstreamExitStatus()
    {
        var r=Run("--noEmit","--noLib",Write("a.ts","let n: number = \"x\";"),Lib());
        Assert.Multiple(()=>{Assert.That(r.Code,Is.EqualTo(2));Assert.That(r.Out,Does.Contain("(1,5): error TS2322: Type 'string' is not assignable to type 'number'."));});
    }
    [Test] public void ProjectPathsAreRelativeToConfig()
    {
        Write("a.ts","let n: number = 1;");Lib();
        var p=Write("tsconfig.json","{\"files\":[\"a.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.That(Run("-p",p).Code,Is.Zero);
    }
    [TestCase("{broken")]
    [TestCase("{\"files\":[3]}")]
    [TestCase("{\"files\":[],\"extends\":\"./base.json\"}")]
    [TestCase("{\"files\":[],\"compilerOptions\":{\"strict\":true}}")]
    public void InvalidOrUnsupportedConfigFails(string json)=>Assert.That(Run("-p",Write("tsconfig.json",json)).Code,Is.Not.Zero);
    [Test] public void UnsupportedCodeHasNoSuccessExit()=>Assert.That(Run("--noEmit","--noLib",Write("a.ts","let x = 1; x++;"),Lib()).Code,Is.EqualTo(1));
    [Test] public void MissingGlobalsAreReported()=>Assert.That(Run("--noEmit","--noLib",Write("a.ts","let x = 1;")).Out,Does.Contain("TS2318"));
    [Test] public void UnsupportedAmbientMembersAreRejected()=>Assert.That(Run("--noEmit","--noLib",Write("a.ts","let x = 1;"),Write("bad.d.ts","interface Object { x: number } ")).Code,Is.EqualTo(1));
    [Test] public void MissingGlobalsPrecedeCheckerGrammarErrors()
    {
        var r=Run("--noEmit","--noLib",Write("a.ts","const n: number;"));
        Assert.That(r.Out,Does.Contain("TS2318").And.Not.Contain("TS1155"));
    }
}
