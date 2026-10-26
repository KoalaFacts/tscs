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
        var seen=new HashSet<string>(host.FileNameComparer);
        var loaded=new List<SourceInput>(paths.Count);
        foreach(var path in paths)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Source paths must not be empty.",nameof(paths));
            var canonical=host.GetCanonicalFileName(path);
            if(string.IsNullOrWhiteSpace(canonical))
                throw new ArgumentException("The compiler host returned an empty canonical file name.",nameof(host));
            if(!seen.Add(canonical))
                throw new ArgumentException("Duplicate canonical source files are not supported.",nameof(paths));
            var text=host.ReadFile(canonical);
            ArgumentNullException.ThrowIfNull(text);
            loaded.Add(new SourceInput(canonical,text));
        }
        return new CompilerProgram([.. loaded]);
    }

    public CompilationAnalysis Analyze() => analysis ??= Compilation.AnalyzeFiles(sources);
}
