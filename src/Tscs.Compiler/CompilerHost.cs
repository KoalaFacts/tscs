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
/// A single immediate directory child reported by a project-aware source host.
/// Paths use the same canonical identity scheme as ReadFile; virtual hosts
/// can report links without ever consulting the operating system.
/// </summary>
public readonly record struct CompilerDirectoryEntry(string Path,bool IsDirectory,bool IsSymbolicLink);

/// <summary>
/// Optional project directory traversal capability. Existing ICompilerHost
/// implementations remain valid for explicit-file compilation. A VFS opts
/// into glob discovery by implementing this interface with its own entries.
/// </summary>
public interface IProjectSourceHost : ICompilerHost
{
    IEnumerable<CompilerDirectoryEntry> EnumerateDirectory(string canonicalDirectoryName);

    // Project entry points may name a directory instead of tsconfig.json.
    // This host-owned query avoids asking the process filesystem about a VFS.
    // Existing directory-enumeration hosts remain source-compatible when
    // their callers pass the complete configuration filename.
    bool DirectoryExists(string canonicalDirectoryName) => false;
}

/// <summary>
/// Filesystem implementation of the source host. Full paths normalize "." and
/// ".."; symlink resolution and module resolution are intentionally not claimed.
/// </summary>
public sealed class FileSystemCompilerHost : IProjectSourceHost
{
    public StringComparer FileNameComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public string GetCanonicalFileName(string path) => Path.GetFullPath(path);
    public string ReadFile(string canonicalFileName) => File.ReadAllText(canonicalFileName);
    public bool DirectoryExists(string canonicalDirectoryName) => Directory.Exists(canonicalDirectoryName);

    public IEnumerable<CompilerDirectoryEntry> EnumerateDirectory(string canonicalDirectoryName)
    {
        foreach(var entry in Directory.EnumerateFileSystemEntries(canonicalDirectoryName))
        {
            var attributes=File.GetAttributes(entry);
            yield return new CompilerDirectoryEntry(GetCanonicalFileName(entry),
                (attributes & FileAttributes.Directory)!=0,
                (attributes & FileAttributes.ReparsePoint)!=0);
        }
    }
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
    public CompilerOptions Options { get; }

    private CompilerProgram(SourceInput[] inputs,Dictionary<string,Dictionary<int,string>> imports,
        Dictionary<string,ParsedSourceResult> syntax,StringComparer comparer,CompilerOptions options)
    {
        Options=options;
        sources=inputs;
        importPaths=imports;
        syntaxSnapshots=syntax;
        fileNameComparer=comparer;
        Sources=Array.AsReadOnly(inputs);
    }

    public IReadOnlyList<SourceInput> Sources { get; }

    // Preserve the original two-parameter CLR method signature for hosts
    // already compiled against the bootstrap CompilerProgram API.
    public static CompilerProgram Load(ICompilerHost host,IReadOnlyList<string> paths) =>
        Load(host,paths,null);

    public static CompilerProgram Load(ICompilerHost host,IReadOnlyList<string> paths,CompilerOptions? options)
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
                void QueueModule(Token modulePath)
                {
                    var specifier=modulePath.Value ?? "";
                    if(!((specifier.StartsWith("./",StringComparison.Ordinal) ||
                          specifier.StartsWith("../",StringComparison.Ordinal)) &&
                         !specifier.Contains('\\') && !specifier.Contains(':')))
                        throw new ArgumentException(
                            $"Only relative TypeScript module specifiers are supported: '{specifier}' in '{canonical}'.",nameof(paths));
                    // TypeScript's source graph resolves runtime .js module
                    // specifiers against .ts sources while preserving the
                    // original .js specifier for ES2015 output.
                    if(Path.GetExtension(specifier).Length==0)specifier+=".ts";
                    else if(specifier.EndsWith(".js",StringComparison.Ordinal))
                        specifier=specifier[..^3]+".ts";
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
                    resolved.Add(modulePath.Start,dependency);
                    if(scheduled.Add(dependency))queue.Enqueue(dependency);
                }
                foreach(var import in parsed.Root.Statements.OfType<ImportDeclaration>())
                    QueueModule(import.ModulePath);
                foreach(var export in parsed.Root.Statements.OfType<ExportDeclaration>())
                    if(export.ModulePath is { } from)QueueModule(from);
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
        return new CompilerProgram([.. loaded],importPaths,syntax,host.FileNameComparer,options ?? new CompilerOptions());
    }

    public CompilationAnalysis Analyze() => analysis ??= Compilation.AnalyzeFiles(sources,importPaths,fileNameComparer,syntaxSnapshots);

    /// <summary>
    /// Emit the already-parsed, fully checked global-script subset as ES2015
    /// JavaScript. This cannot emit modules, declaration files, unsupported
    /// syntax or any program with errors. It never loads or reparses source.
    /// </summary>
    // Preserve the original EmitJavaScript(bool) binary entry point for hosts
    // built against the bootstrap API; the parameterless path reads Options.
    public IReadOnlyList<JavaScriptOutput> EmitJavaScript(bool emitModules) =>
        EmitJavaScript((bool?)emitModules);

    public IReadOnlyList<JavaScriptOutput> EmitJavaScript(bool? emitModules=null)
    {
        if(Options.NoEmit)
            throw new InvalidOperationException("JavaScript output is disabled by noEmit.");
        // Existing explicit API callers may select the old bool overload;
        // ordinary Program emission uses the same typed effective options as CLI.
        bool emitNamedModules=emitModules ?? Options.Module==CompilerModule.Es2015;
        var checkedProgram=Analyze();
        if(checkedProgram.HasSyntaxErrors || checkedProgram.Diagnostics.Count>0)
            throw new InvalidOperationException("JavaScript output requires a successful checked program.");
        var output=new JavaScriptOutput[sources.Length];
        for(int i=0;i<sources.Length;i++)
        {
            var source=sources[i];
            if(!syntaxSnapshots.TryGetValue(source.File,out var syntax) || syntax.Source is not { } parsed)
                throw new InvalidOperationException("No validated AST snapshot exists for the source.");
            if(!emitNamedModules && parsed.Root.Statements.Any(s=>
                s is ImportDeclaration or ExportDeclaration or Variable { Exported: true } or FunctionDeclaration { Exported: true }))
                throw new NotSupportedException("ES module JavaScript output is not implemented; specify --module es2015 for supported named modules.");
            output[i]=new JavaScriptOutput(source.File,JavaScriptEmitter.Emit(parsed.Root,emitNamedModules));
        }
        return Array.AsReadOnly(output);
    }
}

/// <summary>One emitted JavaScript result paired with its exact source identity.</summary>
public sealed record JavaScriptOutput(string SourceFile,string Text);
