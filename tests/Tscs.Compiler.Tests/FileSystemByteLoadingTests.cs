using System.Text;
using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class FileSystemByteLoadingTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-bytes-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);

    private string Write(string name,byte[] bytes)
    {
        var path=Path.Combine(directory,name);
        File.WriteAllBytes(path,bytes);
        return path;
    }

    private static byte[] Encode(string text,string format)
    {
        return format switch
        {
            "utf8-bom"=>[0xef,0xbb,0xbf,..Encoding.UTF8.GetBytes(text)],
            "utf16-le"=>[0xff,0xfe,..Encoding.Unicode.GetBytes(text)],
            "utf16-be"=>[0xfe,0xff,..Encoding.BigEndianUnicode.GetBytes(text)],
            _=>throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private (int Exit,string Output) Run(params string[] args)
    {
        using var output=new StringWriter();
        var exit=CommandLine.Run(args,output,output);
        return (exit,output.ToString());
    }

    [Theory]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    public void FilesystemHostDecodesSourceAndGlobalsBeforeChecking(string format)
    {
        const string source="let result: number = shared + 2; // 😀\n";
        var a=Write("a.ts",Encode(source,format));
        var b=Write("b.ts",Encode("let shared: number = 40;\n",format));
        var lib=Write("minimal.d.ts",Encode(
            "interface Array<T> {} interface Boolean {} interface Function {} " +
            "interface CallableFunction {} interface NewableFunction {} interface IArguments {} " +
            "interface Number {} interface Object {} interface RegExp {} interface String {}",format));
        var host=new FileSystemCompilerHost();
        Assert.Equal(source,host.ReadFile(a));
        var program=CompilerProgram.Load(host,[a,b]);
        Assert.Equal(source,program.Sources[0].Text);
        Assert.Empty(program.Analyze().Diagnostics);
        var compiled=Run("--noEmit","--noLib","--target","es2015",a,b,lib);
        Assert.Equal(0,compiled.Exit);
        Assert.Equal("",compiled.Output);
    }

    [Fact]
    public void Utf16SourceSemanticErrorStillBlocksEmission()
    {
        var bad=Write("invalid.ts",Encode("let value: number = \"bad\";\n","utf16-be"));
        var lib=Write("minimal.d.ts",Encode(
            "interface Array<T> {} interface Boolean {} interface Function {} " +
            "interface CallableFunction {} interface NewableFunction {} interface IArguments {} " +
            "interface Number {} interface Object {} interface RegExp {} interface String {}","utf8-bom"));
        var result=Run("--emitJs","--noLib","--target","es2015",bad,lib);
        Assert.Equal(2,result.Exit);
        Assert.Contains("TS2322",result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(bad,".js")));
    }

    [Fact]
    public void MalformedUtf8FailsClosedBeforeParsingOrEmitting()
    {
        var bad=Write("malformed.ts",[..Encoding.UTF8.GetBytes("let value = "),0xc3,0x28,(byte)';']);
        var host=new FileSystemCompilerHost();
        Assert.Throws<DecoderFallbackException>(()=>host.ReadFile(bad));
        var result=Run("--emitJs","--noLib","--target","es2015",bad);
        Assert.Equal(1,result.Exit);
        Assert.Contains("TSCS1002",result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(bad,".js")));
    }
}
