namespace Tscs;

public sealed record SourceInput(string File,string Text);
public sealed record CompilationAnalysis(IReadOnlyList<Diagnostic> Diagnostics,bool HasSyntaxErrors,bool EmptyProgram=false);
internal sealed record ParsedSource(string File,Block Root,Scanner Scanner);
// Store the successful AST or the original syntax diagnostics once per source.
internal sealed record ParsedSourceResult(ParsedSource? Source,Diagnostic[] Diagnostics,bool HasSyntaxErrors);

public static class Compilation
{
    public static IReadOnlyList<Diagnostic> Check(string file,string text)=>Analyze(file,text).Diagnostics;
    public static IReadOnlyList<Diagnostic> CheckFiles(IReadOnlyList<SourceInput> files)=>AnalyzeFiles(files).Diagnostics;
    public static CompilationAnalysis Analyze(string file,string text)=>AnalyzeFiles([new(file,text)]);

    // Parse before binding; reuse the host Program's dependency-graph syntax
    // snapshots when present. General module semantics and emit are not claimed.
    public static CompilationAnalysis AnalyzeFiles(IReadOnlyList<SourceInput> files) =>
        AnalyzeFiles(files,null,StringComparer.Ordinal);

    internal static CompilationAnalysis AnalyzeFiles(IReadOnlyList<SourceInput> files,
        IReadOnlyDictionary<string,Dictionary<int,string>>? resolvedImports,StringComparer comparer,
        IReadOnlyDictionary<string,ParsedSourceResult>? syntaxSnapshots=null)
    {
        ArgumentNullException.ThrowIfNull(files);
        if(files.Count==0)throw new ArgumentException("At least one source file is required.",nameof(files));
        if(files.Select(f=>f.File).Distinct(comparer).Count()!=files.Count)
            throw new ArgumentException("Duplicate source paths are not supported.",nameof(files));

        var parsed=new List<ParsedSource>(files.Count);
        var parsingDiagnostics=new List<Diagnostic>();
        bool hasSyntaxErrors=false;
        foreach(var source in files)
        {
            var syntax=syntaxSnapshots!=null && syntaxSnapshots.TryGetValue(source.File,out var cached)
                ? cached : ParseSource(source);
            if(syntax.Source is { } parsedSource)parsed.Add(parsedSource);
            if(syntax.Diagnostics.Length>0)parsingDiagnostics.AddRange(syntax.Diagnostics);
            hasSyntaxErrors |= syntax.HasSyntaxErrors;
        }
        // A broken source cannot safely contribute symbols, but errors in other
        // files must remain visible rather than disappearing behind the first one.
        if(parsingDiagnostics.Count>0)return new(Order(parsingDiagnostics),hasSyntaxErrors);
        Scope? global=null;
        Dictionary<string,Scope>? modules=null;
        try
        {
            bool IsModule(ParsedSource source) =>
                source.Root.Statements.Any(s=>s is ImportDeclaration or ExportDeclaration ||
                    s is Variable { Exported:true } or FunctionDeclaration { Exported:true } or
                    TypeAliasDeclaration { Exported:true } or InterfaceDeclaration { Exported:true });
            var moduleSources=parsed.Where(IsModule).ToArray();
            var scriptSources=parsed.Where(s=>!IsModule(s)).ToArray();
            if(parsed.Count>1 || moduleSources.Length>0)
                global=Binder.BindProgram(scriptSources.Select(p=>(p.File,p.Root)));
            if(moduleSources.Length>0)
                modules=ModuleLinker.Link(moduleSources,global!,resolvedImports,comparer);
        }
        catch(CompilationFailure failure)
        {
            return new([failure.Diagnostic],false);
        }
        var diagnostics=new List<Diagnostic>();
        foreach(var source in parsed)
        {
            try
            {
                var checker=new TypeChecker(source.File,
                    strictModule:modules!=null && modules.ContainsKey(source.File));
                var scope=modules!=null && modules.TryGetValue(source.File,out var local)
                    ? local : global;
                var checkedDiagnostics=scope==null
                    ? checker.Check(source.Root)
                    : checker.CheckBound(source.Root,scope);
                diagnostics.AddRange(Order(checkedDiagnostics.Where(d=>!source.Scanner.SuppressesSemanticDiagnostic(d.Start))));
            }
            catch(CompilationFailure failure)
            {
                diagnostics.Add(failure.Diagnostic);
                break;
            }
        }
        return new(diagnostics,false,parsed.All(p=>p.Root.Statements.Count==0));
    }
    // The dependency loader and Compilation share this actual syntax pass.
    // Unsupported constructs retain their diagnostic; no failure becomes SKIP.
    internal static ParsedSourceResult ParseSource(SourceInput source)
    {
        ArgumentNullException.ThrowIfNull(source.Text);
        bool syntaxPhase=true;
        Scanner? scanner=null;
        try
        {
            if(source.Text.Length>1_000_000)
                throw new CompilationFailure(Diagnostic.Error(source.File,new(TokenKind.End,"",0,0),
                    "TSCS1001","Source exceeds the bootstrap size limit."));
            scanner=new Scanner(source.File,source.Text);
            var tokens=scanner.Scan();
            if(scanner.Diagnostics.Count>0)
                return new ParsedSourceResult(null,scanner.Diagnostics.ToArray(),true);
            var parser=new Parser(source.File,tokens);
            var block=parser.Parse();
            if(parser.Diagnostics.Count>0)
                return new ParsedSourceResult(null,parser.Diagnostics.ToArray(),true);
            syntaxPhase=false;
            return new ParsedSourceResult(new ParsedSource(source.File,block,scanner),[],false);
        }
        catch(CompilationFailure failure)
        {
            var lexical=scanner?.Diagnostics ?? [];
            var errors=new List<Diagnostic>(lexical){failure.Diagnostic};
            return new ParsedSourceResult(null,errors.ToArray(),lexical.Count>0 ||
                (syntaxPhase && !failure.Diagnostic.Code.StartsWith("TSCS",StringComparison.Ordinal)));
        }
    }

    private static Diagnostic[] Order(IEnumerable<Diagnostic> diagnostics)=>
        diagnostics.OrderBy(d=>d.File,StringComparer.Ordinal).ThenBy(d=>d.Start)
            .ThenBy(d=>d.Length).ThenBy(d=>d.Code,StringComparer.Ordinal).ToArray();
}
