using Tscs;
using Xunit;

namespace Tscs.Tests;

// This is a real in-memory project tree: no Directory/File disk reads.
// Its enumerator and ReadFile feed the same CompilerProgram source graph.
public sealed class ProjectVirtualHostTests
{
    private sealed class VirtualProjectHost : IProjectSourceHost
    {
        public string Root { get; }=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"tscs-virtual-project"));
        public StringComparer FileNameComparer => StringComparer.OrdinalIgnoreCase;
        public Dictionary<string,string> Files { get; }=new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Links { get; }=new(StringComparer.OrdinalIgnoreCase);
        public List<string> Reads { get; }=[];

        public string GetCanonicalFileName(string path)=>Path.GetFullPath(path);
        public string PathFor(string relative)=>GetCanonicalFileName(Path.Combine(Root,relative));
        public void Add(string relative,string text)=>Files.Add(PathFor(relative),text);
        public string ReadFile(string canonicalFileName)
        {
            Reads.Add(canonicalFileName);
            return Files[canonicalFileName];
        }

        public IEnumerable<CompilerDirectoryEntry> EnumerateDirectory(string canonicalDirectoryName)
        {
            var children=new Dictionary<string,CompilerDirectoryEntry>(FileNameComparer);
            foreach(var file in Files.Keys)
            {
                string relative=Path.GetRelativePath(canonicalDirectoryName,file)
                    .Replace(Path.DirectorySeparatorChar,'/');
                if(relative==".."||relative.StartsWith("../",StringComparison.Ordinal)||
                   Path.IsPathRooted(relative))continue;
                int slash=relative.IndexOf('/');
                string name=slash<0?relative:relative[..slash];
                if(name.Length==0||name==".")continue;
                var child=GetCanonicalFileName(Path.Combine(canonicalDirectoryName,name));
                children[child]=new CompilerDirectoryEntry(child,slash>=0,Links.Contains(child));
            }
            return children.Values;
        }
    }

    [Fact]
    public void VirtualGlobsResolveRealProgramSourcesWithoutDiskAccess()
    {
        var host=new VirtualProjectHost();
        host.Add("src/first.ts","let first: number = 21;");
        host.Add("src/deep/second.ts","let second: number = first + first;");
        host.Add("src/generated/bad.ts","let bad: number = \"wrong\";");
        host.Add("src/node_modules/bad.ts","let another: number = \"wrong\";");
        host.Add("node_modules/special/part.ts","let part: number = second;");
        host.Add("node_modules/special/node_modules/bad.ts","let badNested: number = \"wrong\";");
        host.Add("src/linked/bad.ts","let linkBad: number = \"wrong\";");
        host.Links.Add(host.PathFor("src/linked"));

        var discovered=ProjectSourceDiscovery.Discover(host,host.Root,[],
            ["src/**/*.ts","node_modules/special/**/*.ts"],["src/gen*"]);
        Assert.Equal(3,discovered.Count);
        Assert.Contains(host.PathFor("src/first.ts"),discovered);
        Assert.Contains(host.PathFor("src/deep/second.ts"),discovered);
        Assert.Contains(host.PathFor("node_modules/special/part.ts"),discovered);

        var result=CompilerProgram.Load(host,discovered).Analyze();
        Assert.Empty(result.Diagnostics);
        Assert.Equal(3,host.Reads.Count);
    }

    [Fact]
    public void VirtualHostCasePolicyControlsGlobMatching()
    {
        var host=new VirtualProjectHost();
        host.Add("src/MAIN.TS","let answer: number = 42;");
        var discovered=ProjectSourceDiscovery.Discover(host,host.Root,[],
            ["SRC/*.ts"],[]);
        Assert.Equal([host.PathFor("src/MAIN.TS")],discovered);
        Assert.Empty(CompilerProgram.Load(host,discovered).Analyze().Diagnostics);
    }

    [Fact]
    public void ExplicitRootInsideVirtualExcludedDirectoryStillReachesChecker()
    {
        var host=new VirtualProjectHost();
        host.Add("src/generated/manual.ts","let manual: number = \"wrong\";");
        host.Add("src/okay.ts","let okay: number = 1;");
        var explicitPath=host.PathFor("src/generated/manual.ts");
        var discovered=ProjectSourceDiscovery.Discover(host,host.Root,[explicitPath],
            ["src/**/*.ts"],["src/gen*"]);
        Assert.Equal([host.PathFor("src/okay.ts")],discovered);
        var result=CompilerProgram.Load(host,[explicitPath,..discovered]).Analyze();
        Assert.Contains(result.Diagnostics,d=>d.Code=="TS2322"&&d.File==explicitPath);
    }
}
