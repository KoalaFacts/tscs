using Tscs;
using Xunit;

namespace Tscs.Tests;

// Real project configuration drives the existing module linker + emitter;
// these are local product regressions, not upstream TS7 acceptance.
public sealed class ProjectModuleOptionsTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-module-config-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);

    private string Write(string name,string text)
    {
        var path=Path.Combine(directory,name);
        File.WriteAllText(path,text);
        return path;
    }

    private string Globals()=>Write("minimal.d.ts",
        "interface Array<T> {} interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");

    private (int Code,string Output) Run(params string[] args)
    {
        using var output=new StringWriter();
        int code=CommandLine.Run(args,output,output);
        return (code,output.ToString());
    }

    [Fact]
    public void ProjectModuleAndTargetOptionsEmitTransitiveNamedEs2015Graph()
    {
        Globals();
        var dependency=Write("value.ts","export const value: number = 21;");
        var entry=Write("entry.ts",
            "import { value } from \"./value.js\"; export const answer: number = value + value;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"entry.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"module\":\"ES6\",\"target\":\"es2015\",\"noLib\":true}}");
        var result=Run("--emitJs","-p",config);
        Assert.Equal(0,result.Code);
        Assert.Equal("",result.Output);
        Assert.Equal("export const value = 21;\n",File.ReadAllText(Path.ChangeExtension(dependency,".js")));
        Assert.Equal("import { value } from \"./value.js\";\nexport const answer = value + value;\n",
            File.ReadAllText(Path.ChangeExtension(entry,".js")));
    }

    [Theory]
    [InlineData("commonjs",1,"Only module es2015/es6")]
    [InlineData("invalid-mode",2,"TS6046")]
    public void UnsupportedOrInvalidProjectModuleFailsWithoutOutput(string module,int exit,string expected)
    {
        Globals();
        var input=Write("entry.ts","export const answer: number = 42;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"entry.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"module\":\""+module+"\",\"target\":\"es2015\",\"noLib\":true}}");
        var result=Run("--emitJs","-p",config);
        Assert.Equal(exit,result.Code);
        Assert.Contains(expected,result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(input,".js")));
    }

    [Fact]
    public void NullProjectModuleDoesNotImplicitlyEnableEmitAndExplicitCliCanOverride()
    {
        Globals();
        var input=Write("entry.ts","export const answer: number = 42;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"entry.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"module\":null,\"target\":\"es2015\",\"noLib\":true}}");
        var rejected=Run("--emitJs","-p",config);
        Assert.Equal(1,rejected.Code);
        Assert.False(File.Exists(Path.ChangeExtension(input,".js")));
        var accepted=Run("--emitJs","--module","es2015","-p",config);
        Assert.Equal(0,accepted.Code);
        Assert.Equal("export const answer = 42;\n",File.ReadAllText(Path.ChangeExtension(input,".js")));
    }
}
