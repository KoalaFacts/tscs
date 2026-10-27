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

    public static IReadOnlyList<string> Discover(string configDirectory,
        IReadOnlyList<string> explicitFiles,
        IReadOnlyList<string> includePatterns,IReadOnlyList<string> excludePatterns)
    {
        if(includePatterns.Count+excludePatterns.Count>MaxPatterns)
            throw new InputFailure("Project source pattern count exceeds the bootstrap limit.");

        // Validate exclusions even if the configuration has no include rules.
        var includes=includePatterns.Select(Compile).ToArray();
        var excludes=excludePatterns.Select(Compile).ToArray();
        if(includes.Length==0)return [];

        var comparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        var seenFiles=new HashSet<string>(explicitFiles,comparer);
        var discovered=new List<string>();
        var directories=new Stack<string>();
        directories.Push(configDirectory);
        int scanned=0;
        // TypeScript excludes package directories by default, unless the
        // include pattern names one explicitly. Never traverse Git metadata.
        bool includePackages=includePatterns.Any(p=>p.Replace('\\','/').Split('/')
            .Any(s=>s is "node_modules" or "bower_components" or "jspm_packages"));
        while(directories.Count>0)
        {
            string directory=directories.Pop();
            foreach(string path in Directory.EnumerateFileSystemEntries(directory))
            {
                if(++scanned>MaxScannedEntries)
                    throw new InputFailure("Project source discovery exceeds the bootstrap scan limit.");
                var attributes=File.GetAttributes(path);
                // Avoid following recursive symlink directories or selecting
                // symlinked files whose identity escapes this bounded host.
                if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                if((attributes&FileAttributes.Directory)!=0)
                {
                    var name=Path.GetFileName(path);
                    if(name is ".git" or ".artifacts")continue;
                    if(!includePackages&&name is "node_modules" or "bower_components" or "jspm_packages")
                        continue;
                    directories.Push(path);
                    continue;
                }
                if(!path.EndsWith(".ts",OperatingSystem.IsWindows()?
                    StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))continue;
                string relative=Path.GetRelativePath(configDirectory,path)
                    .Replace(Path.DirectorySeparatorChar,'/');
                if(!includes.Any(g=>g.IsMatch(relative))||excludes.Any(g=>g.IsMatch(relative)))
                    continue;
                var canonical=Path.GetFullPath(path);
                if(!seenFiles.Add(canonical))continue;
                discovered.Add(canonical);
                if(seenFiles.Count>MaxSourceFiles)
                    throw new InputFailure("Project source file count exceeds the bootstrap limit.");
            }
        }
        discovered.Sort(comparer);
        return discovered;
    }

    private static Regex Compile(string input)
    {
        // Only safe, project-local filesystem globs are accepted here. An
        // explicit "files" entry retains the existing relative-path behavior.
        string pattern=input.Replace('\\','/').TrimEnd('/');
        if(pattern.Length==0||pattern.Length>256||pattern.StartsWith('/')||
           pattern.Contains(':')||pattern.IndexOfAny(['[',']','{','}','\0'])>=0||
           pattern.Contains("//",StringComparison.Ordinal))
            throw new InputFailure($"Unsupported project source pattern '{input}'.");
        var segments=pattern.Split('/');
        if(segments.Any(segment=>segment is "" or "." or ".." ||
           (segment.Contains("**",StringComparison.Ordinal)&&segment!="**")))
            throw new InputFailure($"Unsupported project source pattern '{input}'.");

        // A plain directory name means its descendant sources, just as a
        // conventional tsconfig include/exclude directory entry would.
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
            foreach(char c in segments[i])
            {
                if(c=='*')expression.Append("[^/]*");
                else if(c=='?')expression.Append("[^/]");
                else expression.Append(Regex.Escape(c.ToString()));
            }
        }
        expression.Append('$');
        var options=RegexOptions.CultureInvariant|RegexOptions.NonBacktracking;
        if(OperatingSystem.IsWindows())options|=RegexOptions.IgnoreCase;
        return new Regex(expression.ToString(),options,TimeSpan.FromSeconds(1));
    }
}
