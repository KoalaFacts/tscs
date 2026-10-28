using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class AmbientArrayGlobalsTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-arrays-global-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);

    private (int Exit,string Output) Compile(string arrayDeclaration,string source)
    {
        var file=Path.Combine(directory,"input.ts");
        var globals=Path.Combine(directory,"minimal.d.ts");
        File.WriteAllText(file,source);
        File.WriteAllText(globals,arrayDeclaration+"\n"+
            "interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} "+
            "interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");
        using var stdout=new StringWriter();
        using var stderr=new StringWriter();
        var result=CommandLine.Run(["--noEmit","--noLib","--target","es2015",file,globals],stdout,stderr);
        return(result,stdout.ToString()+stderr.ToString());
    }

    [Theory]
    [InlineData("interface Array<T> { length: number; [index: number]: T; }")]
    [InlineData("interface Array < T > {[n:number]:T;length:number;}")]
    [InlineData("interface Array<T> {\n length:number;\n[position:number]:T;\n }")]
    public void ArrayIndexAndLengthContractSupportsTypedPrograms(string declaration)
    {
        var (code,output)=Compile(declaration,"const items: number[] = [20,22]; let n: number = items[0] + items.length;");
        Assert.Equal(0,code);
        Assert.Equal("",output);
    }

    [Theory]
    [InlineData("interface Array<T> { length: string; [index:number]:T; }")]
    [InlineData("interface Array<T> { length: number; }")]
    [InlineData("interface Array<T> { [index:number]:T; }")]
    [InlineData("interface Array<T> { length: number; length: number; [index:number]:T; }")]
    [InlineData("interface Array<T> { length: number; [index:string]:T; }")]
    [InlineData("interface Array<T> { length: number; [index:number]:any; }")]
    [InlineData("interface Array<T> { length: number; [index:number]:T; pop():T; }")]
    [InlineData("interface Array<T> { length: number; [index:number]:T; } interface Array<T> {}")]
    [InlineData("interface Array<T> { length: number; [index:number]:T; } interface Object { x:number; }")]
    public void UnexpectedAmbientSemanticsNeverSilentlySucceed(string declaration)
    {
        var (code,output)=Compile(declaration,"const xs: number[] = [1,2]; let n: number = xs[0];");
        Assert.Equal(1,code);
        Assert.Contains("TSCS1002",output);
    }

    [Fact]
    public void OriginalEmptyAmbientArrayContractRemainsAccepted()
    {
        var (code,output)=Compile("interface Array<T> {}","let n: number = 1;");
        Assert.Equal(0,code);
        Assert.Equal("",output);
    }
}
