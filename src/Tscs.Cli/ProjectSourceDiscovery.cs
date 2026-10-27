using System.Text;
using System.Text.RegularExpressions;

namespace Tscs;

/// <summary>
/// Bounded filesystem source discovery for explicitly configured tsconfig
/// include/exclude globs. Explicit "files" roots are never excluded.
/// This is not the full TS7 config expansion or symlink resolver.
/// </summary>
internal static class ProjectSourceDiscovery
{
    private const int MaxScannedEntries=50_000;
    private const int MaxSourceFiles=4_096;
    private const int MaxPatterns=64;
    private static StringComparison FileComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static RegexOptions GlobOptions(StringComparer comparer) => RegexOptions.CultureInvariant|
        RegexOptions.NonBacktracking|(comparer.Equals("A","a")?RegexOptions.IgnoreCase:RegexOptions.None);

    internal static bool IsTypeScriptFile(string path)=>path.EndsWith(".ts",FileComparison);
    internal static bool IsDeclarationFile(string path)=>path.EndsWith(".d.ts",FileComparison);
    private static bool HasSuffix(string path,string suffix,StringComparer comparer)=>
        path.Length>=suffix.Length&&comparer.Equals(path[^suffix.Length..],suffix);
    private static bool IsPackageDirectory(string name,StringComparer comparer)=>
        comparer.Equals(name,"node_modules")||
        comparer.Equals(name,"bower_components")||
        comparer.Equals(name,"jspm_packages");

    public static IReadOnlyList<string> Discover(string configDirectory,
        IReadOnlyList<string> explicitFiles,
        IReadOnlyList<string> includePatterns,IReadOnlyList<string> excludePatterns) =>
        Discover(new FileSystemCompilerHost(),configDirectory,explicitFiles,includePatterns,excludePatterns);

    // FileSystemCompilerHost and in-memory VFS hosts share one matching and
    // exclusion policy. The host decides identity, case sensitivity, entries
    // and which directories are symbolic links; the matcher never reads disk.
    public static IReadOnlyList<string> Discover(IProjectSourceHost host,string configDirectory,
        IReadOnlyList<string> explicitFiles,
        IReadOnlyList<string> includePatterns,IReadOnlyList<string> excludePatterns)
    {
        ArgumentNullException.ThrowIfNull(host);
        configDirectory=host.GetCanonicalFileName(configDirectory);
        if(includePatterns.Count+excludePatterns.Count>MaxPatterns)
            throw new InputFailure("Project source pattern count exceeds the bootstrap limit.");
        var comparer=host.FileNameComparer;
        var includes=includePatterns.Select(p=>Compile(p,comparer)).ToArray();
        var excludes=excludePatterns.Select(p=>Compile(p,comparer)).ToArray();
        if(includes.Length==0)return [];

        var seenFiles=new HashSet<string>(explicitFiles.Select(host.GetCanonicalFileName),comparer);
        var discovered=new List<string>();
        int scanned=0;
        // Each include owns its traversal. A package path explicitly named
        // by one glob must never turn on package inclusion for other globs.
        foreach(var include in includes)
        {
            var directories=new Stack<string>();
            directories.Push(configDirectory);
            while(directories.Count>0)
            {
                string directory=directories.Pop();
                foreach(var entry in host.EnumerateDirectory(directory))
                {
                    if(++scanned>MaxScannedEntries)
                        throw new InputFailure("Project source discovery exceeds the bootstrap scan limit.");
                    // The VFS decides whether an entry is a link. Never walk it.
                    if(entry.IsSymbolicLink)continue;
                    var path=host.GetCanonicalFileName(entry.Path);
                    // Reject a malformed host that enumerates another directory
                    // (including a parent/ancestor) as an immediate child.
                    if(!comparer.Equals(Path.GetDirectoryName(path),directory))
                        throw new InputFailure("Project source host returned an entry outside its directory.");
                    string relative=Path.GetRelativePath(configDirectory,path)
                        .Replace(Path.DirectorySeparatorChar,'/');
                    if(relative==".."||relative.StartsWith("../",StringComparison.Ordinal)||
                       Path.IsPathRooted(relative))
                        throw new InputFailure("Project source host returned a path outside the project.");
                    if(entry.IsDirectory)
                    {
                        var name=Path.GetFileName(path);
                        if(comparer.Equals(name,".git")||comparer.Equals(name,".artifacts"))continue;
                        // Package boundaries are checked against the actual
                        // matched glob component, not a global pattern flag.
                        // A whole excluded directory must be pruned before
                        // enumerating its children, not merely filtered later.
                        // A wildcard exclude may match the directory name itself
                        // (e.g. src/gen*) without matching a trailing slash.
                        // Prune that whole subtree, like explicit directory excludes.
                        if(excludes.Any(g=>g.IsMatch(relative)||g.IsMatch(relative+"/"))||
                           !include.CanContainDirectory(relative))continue;
                        directories.Push(path);
                        continue;
                    }
                    if(!HasSuffix(path,".ts",comparer)||!include.IsMatch(relative)||
                       excludes.Any(g=>g.IsMatch(relative)))continue;
                    var canonical=path;
                    if(!seenFiles.Add(canonical))continue;
                    discovered.Add(canonical);
                    if(seenFiles.Count>MaxSourceFiles)
                        throw new InputFailure("Project source file count exceeds the bootstrap limit.");
                }
            }
        }
        discovered.Sort(comparer);
        return discovered;
    }

    private sealed class Glob(string[] segments,Regex pathPattern,Regex[] segmentPatterns,
        StringComparer comparer)
    {
        public bool IsMatch(string path)=>pathPattern.IsMatch(path);

        // A directory can be entered only if a matching file could still
        // occur below it. Iterative glob-state propagation avoids recursion
        // through deeply nested paths (including full-segment "**").
        public bool CanContainDirectory(string relativeDirectory)
        {
            var actual=relativeDirectory.Split('/');
            var states=new bool[segments.Length+1];
            states[0]=true;
            void ExpandDoubleStar()
            {
                for(int p=0;p<segments.Length;p++)
                    if(states[p]&&segments[p]=="**")states[p+1]=true;
            }
            ExpandDoubleStar();
            foreach(var directorySegment in actual)
            {
                var next=new bool[segments.Length+1];
                bool isPackage=IsPackageDirectory(directorySegment,comparer);
                for(int p=0;p<segments.Length;p++)
                {
                    if(!states[p])continue;
                    // A wildcard or "**" cannot descend through a package
                    // directory. Only an explicitly spelled segment at
                    // this precise glob position may consume its name.
                    if(segments[p]=="**")
                    {
                        if(!isPackage)next[p]=true;
                    }
                    else if(p<segments.Length-1&&segmentPatterns[p].IsMatch(directorySegment)&&
                        (!isPackage||comparer.Equals(segments[p],directorySegment)))
                        next[p+1]=true;
                }
                states=next;
                ExpandDoubleStar();
            }
            for(int p=0;p<segments.Length;p++)if(states[p])return true;
            return false;
        }
    }

    private static Glob Compile(string input,StringComparer comparer)
    {
        // "files" retains its old path behavior; globs are restricted to
        // project-local, unambiguous filesystem selectors.
        string pattern=input.Replace('\\','/').TrimEnd('/');
        if(pattern.Length==0||pattern.Length>256||pattern.StartsWith('/')||
           pattern.Contains(':')||pattern.IndexOfAny(['[',']','{','}','\0'])>=0||
           pattern.Contains("//",StringComparison.Ordinal))
            throw new InputFailure($"Unsupported project source pattern '{input}'.");
        var segments=pattern.Split('/');
        if(segments.Any(s=>s is "" or "." or ".." ||
           (s.Contains("**",StringComparison.Ordinal)&&s!="**")))
            throw new InputFailure($"Unsupported project source pattern '{input}'.");

        string leaf=segments[^1];
        if(leaf!="**"&&!leaf.Contains('*')&&!leaf.Contains('?')&&!leaf.Contains('.'))
        {
            pattern+="/**";
            segments=pattern.Split('/');
        }

        var expression=new StringBuilder("^");
        for(int i=0;i<segments.Length;i++)
        {
            if(i>0&&segments[i-1]!="**")expression.Append('/');
            if(segments[i]=="**")
            {
                expression.Append(i==segments.Length-1?".*":@"(?:[^/]+/)*");
                continue;
            }
            expression.Append(SegmentExpression(segments[i]));
        }
        expression.Append('$');
        var segmentPatterns=segments.Select(s=>new Regex("^"+SegmentExpression(s)+"$",
            GlobOptions(comparer),TimeSpan.FromSeconds(1))).ToArray();
        return new Glob(segments,new Regex(expression.ToString(),
            GlobOptions(comparer),TimeSpan.FromSeconds(1)),segmentPatterns,comparer);
    }

    private static string SegmentExpression(string segment)
    {
        var output=new StringBuilder();
        foreach(char c in segment)
        {
            if(c=='*')output.Append("[^/]*");
            else if(c=='?')output.Append("[^/]");
            else output.Append(Regex.Escape(c.ToString()));
        }
        return output.ToString();
    }
}
