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
