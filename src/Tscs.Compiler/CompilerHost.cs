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
    private readonly Dictionary<string,Dictionary<int,string>> importPaths;
    private readonly IReadOnlyDictionary<string,ParsedSourceResult> syntaxSnapshots;
    private readonly StringComparer fileNameComparer;
    private CompilationAnalysis? analysis;

    private CompilerProgram(SourceInput[] inputs,Dictionary<string,Dictionary<int,string>> imports,
        Dictionary<string,ParsedSourceResult> syntax,StringComparer comparer)
    {
        sources=inputs;
        importPaths=imports;
        syntaxSnapshots=syntax;
        fileNameComparer=comparer;
        Sources=Array.AsReadOnly(inputs);
    }

    public IReadOnlyList<SourceInput> Sources { get; }

    public static CompilerProgram Load(ICompilerHost host,IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(paths);
        if(paths.Count==0)throw new ArgumentException("At least one source file is required.",nameof(paths));
        // Root aliases remain a caller error, while transitive dependencies
        // reached through diamonds or cycles are read exactly once. Load
        // explicit roots in encounter order, preserving the pre-existing
        // once-read-before-alias-rejection contract.
        var roots=new HashSet<string>(host.FileNameComparer);
        var scheduled=new HashSet<string>(host.FileNameComparer);
        var loadedFiles=new HashSet<string>(host.FileNameComparer);
        var queue=new Queue<string>();
        var loaded=new List<SourceInput>();
        var importPaths=new Dictionary<string,Dictionary<int,string>>(host.FileNameComparer);
        var syntax=new Dictionary<string,ParsedSourceResult>(host.FileNameComparer);

        void LoadOne(string canonical)
        {
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

            // Source edges and semantic checking reuse the same parsed AST.
            // Invalid scanner/parser results also remain stored for Analyze.
            var snapshot=Compilation.ParseSource(new SourceInput(canonical,source));
            syntax.Add(canonical,snapshot);
            if(snapshot.Source is { } parsed)
            {
                foreach(var import in parsed.Root.Statements.OfType<ImportDeclaration>())
                {
                    var specifier=import.ModulePath.Value ?? "";
                    if(!((specifier.StartsWith("./",StringComparison.Ordinal) ||
                          specifier.StartsWith("../",StringComparison.Ordinal)) &&
                         !specifier.Contains('\\') && !specifier.Contains(':')))
                        throw new ArgumentException(
                            $"Only relative TypeScript module specifiers are supported: '{specifier}' in '{canonical}'.",nameof(paths));
                    if(Path.GetExtension(specifier).Length==0)specifier+=".ts";
                    if(!specifier.EndsWith(".ts",StringComparison.Ordinal) ||
                       specifier.EndsWith(".d.ts",StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Only TypeScript source module paths are supported: '{specifier}' in '{canonical}'.",nameof(paths));
                    var dependency=host.ResolveReferencePath(canonical,specifier);
                    if(string.IsNullOrWhiteSpace(dependency))
                        throw new ArgumentException("The compiler host returned an empty module identity.",nameof(host));
                    if(!importPaths.TryGetValue(canonical,out var resolved))
                    {
                        resolved=new Dictionary<int,string>();
                        importPaths.Add(canonical,resolved);
                    }
                    resolved.Add(import.ModulePath.Start,dependency);
                    if(scheduled.Add(dependency))queue.Enqueue(dependency);
                }
            }
        }

        foreach(var path in paths)
        {
            if(string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Source paths must not be empty.",nameof(paths));
            var canonical=host.GetCanonicalFileName(path);
            if(string.IsNullOrWhiteSpace(canonical))
                throw new ArgumentException("The compiler host returned an empty canonical file name.",nameof(host));
            if(!roots.Add(canonical))
                throw new ArgumentException("Duplicate canonical source files are not supported.",nameof(paths));
            scheduled.Add(canonical);
            if(loadedFiles.Add(canonical))LoadOne(canonical);
        }

        while(queue.Count>0)
        {
            var canonical=queue.Dequeue();
            if(loadedFiles.Add(canonical))LoadOne(canonical);
        }
        return new CompilerProgram([.. loaded],importPaths,syntax,host.FileNameComparer);
    }

    public CompilationAnalysis Analyze() => analysis ??= Compilation.AnalyzeFiles(sources,importPaths,fileNameComparer,syntaxSnapshots);
}
