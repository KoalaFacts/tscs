using Tscs;
using Xunit;

namespace Tscs.Tests;

// Runs the real configuration -> discovery -> Program -> checker path without
// making either the configuration or the sources available on disk.
public sealed class ProjectConfigVirtualHostTests
{
    private const string Globals =
        "interface Array<T> {} interface Boolean {} interface Function {} " +
        "interface CallableFunction {} interface NewableFunction {} " +
        "interface IArguments {} interface Number {} interface Object {} " +
        "interface RegExp {} interface String {}";

    private sealed class MemoryProjectHost : IProjectSourceHost
    {
        public string Root { get; } = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "tscs-config-vfs-" + Guid.NewGuid().ToString("N")));
        public StringComparer FileNameComparer => StringComparer.OrdinalIgnoreCase;
        public Dictionary<string,string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Reads { get; } = [];

        public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
        public string FileAt(string relative) => GetCanonicalFileName(Path.Combine(Root, relative));
        public void Add(string relative, string content) => Files.Add(FileAt(relative), content);

        public string ReadFile(string canonicalFileName)
        {
            Reads.Add(canonicalFileName);
            return Files.TryGetValue(canonicalFileName, out var text)
                ? text : throw new FileNotFoundException("No virtual file.", canonicalFileName);
        }

        public bool DirectoryExists(string canonicalDirectoryName)
        {
            var prefix = Path.TrimEndingDirectorySeparator(canonicalDirectoryName) +
                Path.DirectorySeparatorChar;
            return Files.Keys.Any(path =>
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<CompilerDirectoryEntry> EnumerateDirectory(string canonicalDirectoryName)
        {
            var prefix = Path.TrimEndingDirectorySeparator(canonicalDirectoryName) +
                Path.DirectorySeparatorChar;
            var children = new Dictionary<string, CompilerDirectoryEntry>(FileNameComparer);
            foreach(var path in Files.Keys)
            {
                if(!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))continue;
                var relative = path[prefix.Length..];
                int separator = relative.IndexOf(Path.DirectorySeparatorChar);
                string name = separator < 0 ? relative : relative[..separator];
                if(name.Length == 0)continue;
                var child = GetCanonicalFileName(Path.Combine(canonicalDirectoryName, name));
                children[child] = new CompilerDirectoryEntry(child, separator >= 0, false);
            }
            return children.Values;
        }
    }

    private static (int Exit, string Output) Run(MemoryProjectHost host, params string[] args)
    {
        using var writer = new StringWriter();
        int exit = CommandLine.Run(["--json", ..args], writer, writer, host);
        return (exit, writer.ToString());
    }

    [Fact]
    public void DirectoryProjectLoadsConfigAndDiscoveredFilesThroughOneVirtualHost()
    {
        var host = new MemoryProjectHost();
        host.Add("project/tsconfig.json",
            """{"files":["minimal.d.ts"],"include":["src/**/*.ts"],"exclude":["src/generated"],"compilerOptions":{"noEmit":true,"noLib":true}}""");
        host.Add("project/minimal.d.ts", Globals);
        host.Add("project/src/first.ts", "let first: number = 21;");
        host.Add("project/src/deep/second.ts", "let result: number = first + first;");
        host.Add("project/src/generated/bad.ts", "let bad: number = \"wrong\";");

        Assert.False(Directory.Exists(host.Root));
        var result = Run(host, "-p", host.FileAt("project"));
        Assert.Equal(0, result.Exit);
        Assert.Contains("\"diagnostics\":[]", result.Output);
        Assert.Equal(4, host.Reads.Count);
        Assert.Contains(host.FileAt("project/tsconfig.json"), host.Reads);
        Assert.DoesNotContain(host.FileAt("project/src/generated/bad.ts"), host.Reads);
    }

    [Fact]
    public void VirtualHostCaseRulesApplyToConfigFileAndUppercaseTsSuffix()
    {
        var host = new MemoryProjectHost();
        host.Add("tsconfig.json",
            """{"files":["minimal.d.ts"],"include":["SRC/*.ts"],"compilerOptions":{"noEmit":true,"noLib":true}}""");
        host.Add("minimal.d.ts", Globals);
        host.Add("src/MAIN.TS", "let answer: number = 42;");

        var result = Run(host, "-p", host.FileAt("TSCONFIG.JSON"));
        Assert.Equal(0, result.Exit);
        Assert.Contains("\"diagnostics\":[]", result.Output);
        Assert.Contains(host.FileAt("src/MAIN.TS"), host.Reads);
    }

    [Fact]
    public void VirtualConfigSyntaxDiagnosticsUseVirtualSourceLocation()
    {
        var host = new MemoryProjectHost();
        host.Add("tsconfig.json", """{'files':[]}""");

        var result = Run(host, "-p", host.FileAt("tsconfig.json"));
        Assert.Equal(2, result.Exit);
        Assert.Contains("TS1327", result.Output);
        Assert.Contains("\"file\":\"tsconfig.json\"", result.Output);
        Assert.Single(host.Reads);
    }

    [Theory]
    [InlineData("""{"files":[42]}""", "42", false)]
    [InlineData("""{"include":"src/**/*.ts"}""", "\"src/**/*.ts\"", false)]
    [InlineData("""{"compilerOptions":{"strict":true}}""", "\"strict\"", false)]
    [InlineData("""{"compilerOptions":{"noEmit":true,"noEmit":false}}""", "\"noEmit\"", true)]
    [InlineData("""{"compilerOptions":{"module":"commonjs"}}""", "\"commonjs\"", false)]
    [InlineData("""{"extends":"base.json"}""", "\"extends\"", false)]
    [InlineData("""{"compilerOptions":[]}""", "[]", false)]
    public void VirtualConfigValidationErrorsKeepSourceBoundUtf16Spans(
        string config,string marker,bool last)
    {
        var host = new MemoryProjectHost();
        host.Add("tsconfig.json", config);

        var result = Run(host, "-p", host.FileAt("tsconfig.json"));
        Assert.Equal(1, result.Exit);
        using var json = System.Text.Json.JsonDocument.Parse(result.Output);
        Assert.Equal(1, json.RootElement.GetProperty("exitCode").GetInt32());
        var diagnostic = Assert.Single(json.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("TSCS1002", diagnostic.GetProperty("code").GetString());
        Assert.Equal("tsconfig.json", diagnostic.GetProperty("file").GetString());
        int expectedStart = last ? config.LastIndexOf(marker, StringComparison.Ordinal)
            : config.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(expectedStart >= 0);
        Assert.Equal(expectedStart, diagnostic.GetProperty("start").GetInt32());
        Assert.Equal(marker.Length, diagnostic.GetProperty("length").GetInt32());
        Assert.Single(host.Reads);
    }


    // These inputs reach real host-backed project loading; validation must
    // preserve the specific JSON selector token instead of a fileless error.
    [Theory]
    [InlineData("""{"include":["../*.ts"]}""", "\"../*.ts\"")]
    [InlineData("""{"exclude":["src/**thing.ts"]}""", "\"src/**thing.ts\"")]
    [InlineData("""{"include":["src/**/*.ts"],"exclude":["/absolute/*.ts"]}""", "\"/absolute/*.ts\"")]
    [InlineData("""{"include":["src/[bad].ts"]}""", "\"src/[bad].ts\"")]
    public void VirtualSourceSelectorGlobErrorsKeepUtf16ConfigPositions(string config,string selector)
    {
        AssertSourceBoundProjectError(config,selector,"Unsupported project source pattern");
    }

    [Fact]
    public void VirtualProjectSelectorCountLimitPointsToTheSixtyFifthPattern()
    {
        var patterns=Enumerable.Repeat("\"src/*.ts\"",64).Append("\"src/too-many.ts\"");
        var config="{\"include\":["+string.Join(",",patterns)+"]}";
        AssertSourceBoundProjectError(config,"\"src/too-many.ts\"",
            "Project source pattern count exceeds the bootstrap limit");
    }

    [Fact]
    public void VirtualProjectDuplicateExplicitFileUsesTheHostIdentityAndSecondToken()
    {
        AssertSourceBoundProjectError(
            """{"files":["src/Main.ts","src/main.ts"]}""",
            "\"src/main.ts\"","Duplicate input paths are not supported");
    }

    private static void AssertSourceBoundProjectError(string config,string token,string message)
    {
        var host=new MemoryProjectHost();
        host.Add("tsconfig.json",config);
        var result=Run(host,"-p",host.FileAt("tsconfig.json"));
        Assert.Equal(1,result.Exit);
        using var json=System.Text.Json.JsonDocument.Parse(result.Output);
        Assert.Equal(1,json.RootElement.GetProperty("exitCode").GetInt32());
        var diagnostic=Assert.Single(json.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("TSCS1002",diagnostic.GetProperty("code").GetString());
        Assert.Equal("tsconfig.json",diagnostic.GetProperty("file").GetString());
        int start=config.IndexOf(token,StringComparison.Ordinal);
        Assert.True(start>=0);
        Assert.Equal(start,diagnostic.GetProperty("start").GetInt32());
        Assert.Equal(token.Length,diagnostic.GetProperty("length").GetInt32());
        Assert.Contains(message,diagnostic.GetProperty("message").GetString()!);
        Assert.Single(host.Reads);
    }

    [Fact]
    public void VirtualProjectCannotAccidentallyWritePhysicalJavaScriptFiles()
    {
        var host = new MemoryProjectHost();
        host.Add("tsconfig.json",
            """{"files":["entry.ts","minimal.d.ts"],"compilerOptions":{"noLib":true,"target":"es2015","module":"es2015"}}""");
        host.Add("minimal.d.ts", Globals);
        host.Add("entry.ts", "export const answer: number = 42;");

        var result = Run(host, "--emitJs", "-p", host.FileAt("tsconfig.json"));
        Assert.Equal(1, result.Exit);
        Assert.Contains("Filesystem JavaScript output is unavailable", result.Output);
        Assert.False(File.Exists(Path.ChangeExtension(host.FileAt("entry.ts"), ".js")));
    }
}
