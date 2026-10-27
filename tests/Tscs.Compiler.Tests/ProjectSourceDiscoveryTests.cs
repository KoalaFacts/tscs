using Tscs;
using Xunit;

namespace Tscs.Tests;

// Exercises actual CLI -> tsconfig -> source discovery -> Program checking.
// Bounded local regressions, not original pinned upstream acceptance credit.
public sealed class ProjectSourceDiscoveryTests : IDisposable
{
    private readonly string directory=Directory.CreateTempSubdirectory("tscs-discovery-").FullName;
    public void Dispose()=>Directory.Delete(directory,true);

    private string Write(string relative,string source)
    {
        var file=Path.Combine(directory,relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file,source);
        return file;
    }

    private string Globals()=>Write("minimal.d.ts",
        "interface Array<T> {} interface Boolean {} interface Function {} interface CallableFunction {} interface NewableFunction {} interface IArguments {} interface Number {} interface Object {} interface RegExp {} interface String {}");

    private (int Exit,string Output) Run(string config)
    {
        using var writer=new StringWriter();
        int exit=CommandLine.Run(["--json","-p",config],writer,writer);
        return (exit,writer.ToString());
    }

    [Fact]
    public void RecursiveIncludesAndDirectoryExcludesReachTheRealProgram()
    {
        Globals();
        Write("src/first.ts","let first: number = 21;");
        Write("src/deep/second.ts","let second: number = first + first;");
        Write("src/generated/bad.ts","let bad: number = \"not a number\";");
        Write("outside/bad.ts","let another: number = \"not a number\";");
        var config=Write("tsconfig.json",
            "{\"files\":[\"minimal.d.ts\"],\"include\":[\"src/**/*.ts\"],\"exclude\":[\"src/generated\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        var result=Run(config);
        Assert.Equal(0,result.Exit);
        Assert.Contains("\"diagnostics\":[]",result.Output);
    }

    [Fact]
    public void ExcludesDoNotRemoveExplicitFiles()
    {
        Globals();
        Write("src/manual.ts","let manual: number = \"wrong\";");
        Write("src/other.ts","let other: number = 1;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"src/manual.ts\",\"minimal.d.ts\"],\"include\":[\"src/**/*.ts\"],\"exclude\":[\"src/manual.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        var result=Run(config);
        Assert.Equal(2,result.Exit);
        Assert.Contains("TS2322",result.Output);
        Assert.Contains("manual.ts",result.Output);
    }

    [Fact]
    public void SingleDirectoryWildcardDoesNotIncludeNestedInvalidSource()
    {
        Globals();
        Write("src/valid.ts","let valid: number = 1;");
        Write("src/deep/invalid.ts","let invalid: number = \"wrong\";");
        var config=Write("tsconfig.json",
            "{\"files\":[\"minimal.d.ts\"],\"include\":[\"src/*.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.Equal(0,Run(config).Exit);
    }

    [Fact]
    public void IncludeAndExplicitFileOverlapDoesNotProduceDuplicateRoot()
    {
        Globals();
        Write("src/entry.ts","let value: number = 42;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"src/entry.ts\",\"minimal.d.ts\"],\"include\":[\"src/**/*.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.Equal(0,Run(config).Exit);
    }

    [Fact]
    public void PackageDirectoryOptInCannotLeakIntoAnotherInclude()
    {
        Globals();
        Write("src/main.ts","let result: number = packageValue;");
        Write("src/node_modules/bad.ts","let bad: number = \"wrong\";");
        Write("node_modules/special.ts","let packageValue: number = 42;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"minimal.d.ts\"],\"include\":[\"src/**/*.ts\",\"node_modules/special.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        var result=Run(config);
        Assert.Equal(0,result.Exit);
        Assert.DoesNotContain("bad.ts",result.Output);
    }

    [Fact]
    public void DiscoveredSourceAndCliClassifyExtensionCaseConsistently()
    {
        Globals();
        Write("src/extra.TS","let upperValue: number = 42;");
        Write("src/main.ts","let result: number = upperValue;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"minimal.d.ts\"],\"include\":[\"src/*.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        var result=Run(config);
        // Windows hosts match and load .TS, Unix hosts correctly keep that
        // suffix distinct and report the missing global symbol.
        Assert.Equal(OperatingSystem.IsWindows()?0:2,result.Exit);
        if(!OperatingSystem.IsWindows())Assert.Contains("TS2304",result.Output);
    }

    [Theory]
    [InlineData("../*.ts")]
    [InlineData("/absolute/*.ts")]
    [InlineData("src/**thing.ts")]
    [InlineData("src/[bad].ts")]
    public void UnsafeOrUnsupportedPatternsFailClosed(string pattern)
    {
        Globals();
        Write("src/entry.ts","let value: number = 1;");
        var config=Write("tsconfig.json",
            "{\"files\":[\"src/entry.ts\",\"minimal.d.ts\"],\"include\":[\""+pattern+"\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        var result=Run(config);
        Assert.Equal(1,result.Exit);
        Assert.Contains("Unsupported project source pattern",result.Output);
    }

    [Fact]
    public void OnlyExplicitFilesRemainSelectedWhenIncludeIsOmitted()
    {
        Globals();
        Write("src/entry.ts","let value: number = 1;");
        Write("src/invalid.ts","let bad: number = \"wrong\";");
        var config=Write("tsconfig.json",
            "{\"files\":[\"src/entry.ts\",\"minimal.d.ts\"],\"compilerOptions\":{\"noEmit\":true,\"noLib\":true}}");
        Assert.Equal(0,Run(config).Exit);
    }
}
