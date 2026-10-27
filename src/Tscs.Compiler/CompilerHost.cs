namespace Tscs;

/// <summary>
/// Supplies canonical source identities and text to a compilation.
/// A host may be a real filesystem, an editor's in-memory buffers, or a VFS.
/// The comparer must agree with its canonical-name semantics.
/// </summary>
public interface ICompilerHost
{
    StringComparer FileNameComparer { get; }
    string GetCanonicalFileName(string path);
    string ReadFile(string canonicalFileName);

    // Resolve relative source dependencies through the same host identity
    // boundary. Virtual hosts may override this when they do not use OS paths.
    string ResolveReferencePath(string containingFileName, string referencePath)
    {
        var directory=Path.GetDirectoryName(containingFileName);
        if(string.IsNullOrEmpty(directory))
            throw new ArgumentException("Cannot resolve a reference without a source directory.",nameof(containingFileName));
        return GetCanonicalFileName(Path.Combine(directory,referencePath));
    }
}

/// <summary>
/// Filesystem implementation of the source host. Full paths normalize "." and
/// ".."; symlink resolution and module resolution are intentionally not claimed.
/// </summary>
public sealed class FileSystemCompilerHost : ICompilerHost
{
    public StringComparer FileNameComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
    public string ReadFile(string canonicalFileName) => File.ReadAllText(canonicalFileName);
}

/// <summary>
/// Immutable, once-loaded set of source files with stable identities. A program
/// snapshots host contents and caches its analysis; a new program is required
/// after an editor buffer or filesystem file changes.
/// </summary>
public sealed class CompilerProgram
{
    private readonly SourceInput[] sources;
    private CompilationAnalysis? analysis;

    private CompilerProgram(SourceInput[] inputs)
    {
        sources=inputs;
        Sources=Array.AsReadOnly(inputs);
    }

    public IReadOnlyList<SourceInput> Sources { get; }

    public static CompilerProgram Load(ICompilerHost host,IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(paths);
        if(paths.Count==0)throw new ArgumentException("At least one source file is required.",nameof(paths));
        // Root aliases remain a caller error, while dependencies reached by
        // several roots (or in cycles) are loaded exactly once.
        var roots=new HashSet<string>(host.FileNameComparer);
        var scheduled=new HashSet<string>(host.FileNameComparer);
        var queue=new Queue<string>();
        foreach(var path in paths)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Source paths must not be empty.",nameof(paths));
            var canonical=host.GetCanonicalFileName(path);
            if(string.IsNullOrWhiteSpace(canonical))
                throw new ArgumentException("The compiler host returned an empty canonical file name.",nameof(host));
            if(!roots.Add(canonical))
                throw new ArgumentException("Duplicate canonical source files are not supported.",nameof(paths));
            if(scheduled.Add(canonical))queue.Enqueue(canonical);
        }

        var loaded=new List<SourceInput>();
        while(queue.Count>0)
        {
            var canonical=queue.Dequeue();
            var source=host.ReadFile(canonical);
            ArgumentNullException.ThrowIfNull(source);
            if(source.Length>1_000_000)
                throw new ArgumentException($"Source exceeds the bootstrap size limit: '{canonical}'.",nameof(paths));
            loaded.Add(new SourceInput(canonical,source));
            if(loaded.Count>4096)
                throw new ArgumentException("The source dependency graph exceeds the bootstrap file limit.",nameof(paths));

            foreach(var reference in TripleSlashPathReferences.Collect(source))
            {
                // General module/file resolution belongs to the later project
                // host. A referenced path is deliberately relative here.
                if(Path.IsPathRooted(reference.Path) ||
                   reference.Path.StartsWith("//",StringComparison.Ordinal) ||
                   (reference.Path.Length>=2 && char.IsLetter(reference.Path[0]) && reference.Path[1]==':'))
                    throw new ArgumentException(
                        $"Only relative triple-slash paths are supported: '{reference.Path}' in '{canonical}'.",nameof(paths));
                var dependency=host.ResolveReferencePath(canonical,reference.Path);
                if(string.IsNullOrWhiteSpace(dependency))
                    throw new ArgumentException("The compiler host returned an empty dependency identity.",nameof(host));
                if(scheduled.Add(dependency))queue.Enqueue(dependency);
            }
        }
        return new CompilerProgram([.. loaded]);
    }

    public CompilationAnalysis Analyze() => analysis ??= Compilation.AnalyzeFiles(sources);
}
