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
    private static RegexOptions GlobOptions => RegexOptions.CultureInvariant|
        RegexOptions.NonBacktracking|(OperatingSystem.IsWindows()?RegexOptions.IgnoreCase:RegexOptions.None);

    internal static bool IsTypeScriptFile(string path)=>path.EndsWith(".ts",FileComparison);
    internal static bool IsDeclarationFile(string path)=>path.EndsWith(".d.ts",FileComparison);

    public static IReadOnlyList<string> Discover(string configDirectory,
        IReadOnlyList<string> explicitFiles,
        IReadOnlyList<string> includePatterns,IReadOnlyList<string> excludePatterns)
    {
        if(includePatterns.Count+excludePatterns.Count>MaxPatterns)
            throw new InputFailure("Project source pattern count exceeds the bootstrap limit.");
        var includes=includePatterns.Select(Compile).ToArray();
        var excludes=excludePatterns.Select(Compile).ToArray();
        if(includes.Length==0)return [];

        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var seenFiles=new HashSet<string>(explicitFiles,comparer);
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
                foreach(string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if(++scanned>MaxScannedEntries)
                        throw new InputFailure("Project source discovery exceeds the bootstrap scan limit.");
                    var attributes=File.GetAttributes(path);
                    // Do not traverse symlinks and then misattribute file
                    // identity to a canonical path outside this source tree.
                    if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                    string relative=Path.GetRelativePath(configDirectory,path)
                        .Replace(Path.DirectorySeparatorChar,'/');
                    if((attributes&FileAttributes.Directory)!=0)
                    {
                        var name=Path.GetFileName(path);
                        if(name is ".git" or ".artifacts")continue;
                        if(name is "node_modules" or "bower_components" or "jspm_packages")
                            if(!include.ExplicitlyNamesPackageDirectory(name))continue;
                        // A whole excluded directory must be pruned before
                        // enumerating its children, not merely filtered later.
                        if(excludes.Any(g=>g.IsMatch(relative+"/"))||
                           !include.CanContainDirectory(relative))continue;
                        directories.Push(path);
                        continue;
                    }
                    if(!IsTypeScriptFile(path)||!include.IsMatch(relative)||
                       excludes.Any(g=>g.IsMatch(relative)))continue;
                    var canonical=Path.GetFullPath(path);
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

    private sealed class Glob(string[] segments,Regex pathPattern,Regex[] segmentPatterns)
    {
        public bool IsMatch(string path)=>pathPattern.IsMatch(path);

        public bool ExplicitlyNamesPackageDirectory(string name)=>
            segments.Take(segments.Length-1).Any(s=>string.Equals(s,name,FileComparison));

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
                for(int p=0;p<segments.Length;p++)
                {
                    if(!states[p])continue;
                    if(segments[p]=="**")next[p]=true;
                    else if(p<segments.Length-1&&segmentPatterns[p].IsMatch(directorySegment))
                        next[p+1]=true;
                }
                states=next;
                ExpandDoubleStar();
            }
            for(int p=0;p<segments.Length;p++)if(states[p])return true;
            return false;
        }
    }

    private static Glob Compile(string input)
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
            GlobOptions,TimeSpan.FromSeconds(1))).ToArray();
        return new Glob(segments,new Regex(expression.ToString(),
            GlobOptions,TimeSpan.FromSeconds(1)),segmentPatterns);
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
