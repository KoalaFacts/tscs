namespace Tscs;

public sealed record SourceInput(string File,string Text);
public sealed record CompilationAnalysis(IReadOnlyList<Diagnostic> Diagnostics,bool HasSyntaxErrors,bool EmptyProgram=false);
internal sealed record ParsedSource(string File,Block Root,Scanner Scanner);

public static class Compilation
{
    public static IReadOnlyList<Diagnostic> Check(string file,string text)=>Analyze(file,text).Diagnostics;
    public static IReadOnlyList<Diagnostic> CheckFiles(IReadOnlyList<SourceInput> files)=>AnalyzeFiles(files).Diagnostics;
    public static CompilationAnalysis Analyze(string file,string text)=>AnalyzeFiles([new(file,text)]);

    // Parse every source before binding and type checking. The global script
    // declarations are available from every file regardless of input order.
    // Modules, complex inferred cross-file types and general emit are not yet supported.
    public static CompilationAnalysis AnalyzeFiles(IReadOnlyList<SourceInput> files) =>
        AnalyzeFiles(files,null,StringComparer.Ordinal);

    internal static CompilationAnalysis AnalyzeFiles(IReadOnlyList<SourceInput> files,
        IReadOnlyDictionary<string,Dictionary<int,string>>? resolvedImports,StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(files);
        if(files.Count==0)throw new ArgumentException("At least one source file is required.",nameof(files));
        if(files.Select(f=>f.File).Distinct(StringComparer.Ordinal).Count()!=files.Count)
            throw new ArgumentException("Duplicate source paths are not supported.",nameof(files));

        var parsed=new List<ParsedSource>(files.Count);
        var parsingDiagnostics=new List<Diagnostic>();
        bool hasSyntaxErrors=false;
        foreach(var source in files)
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
                {
                    parsingDiagnostics.AddRange(scanner.Diagnostics);
                    hasSyntaxErrors=true;
                    continue;
                }
                var parser=new Parser(source.File,tokens);
                var block=parser.Parse();
                if(parser.Diagnostics.Count>0)
                {
                    parsingDiagnostics.AddRange(parser.Diagnostics);
                    hasSyntaxErrors=true;
                    continue;
                }
                syntaxPhase=false;
                parsed.Add(new(source.File,block,scanner));
            }
            catch(CompilationFailure failure)
            {
                var lexical=scanner?.Diagnostics ?? [];
                parsingDiagnostics.AddRange(lexical);
                parsingDiagnostics.Add(failure.Diagnostic);
                hasSyntaxErrors |= lexical.Count>0 ||
                    (syntaxPhase && !failure.Diagnostic.Code.StartsWith("TSCS",StringComparison.Ordinal));
            }
        }
        // A broken source cannot safely contribute symbols, but errors in other
        // files must remain visible rather than disappearing behind the first one.
        if(parsingDiagnostics.Count>0)return new(Order(parsingDiagnostics),hasSyntaxErrors);
        Scope? global=null;
        Dictionary<string,Scope>? modules=null;
        try
        {
            bool IsModule(ParsedSource source) =>
                source.Root.Statements.Any(s=>s is ImportDeclaration || s is Variable { Exported:true });
            var moduleSources=parsed.Where(IsModule).ToArray();
            var scriptSources=parsed.Where(s=>!IsModule(s)).ToArray();
            if(parsed.Count>1 || moduleSources.Length>0)
                global=Binder.BindProgram(scriptSources.Select(p=>(p.File,p.Root)));
            if(moduleSources.Length>0)
            {
                modules=new Dictionary<string,Scope>(comparer);
                foreach(var source in moduleSources)
                    modules.Add(source.File,Binder.BindModule(source.File,source.Root,global!));
                foreach(var source in moduleSources)
                {
                    foreach(var import in source.Root.Statements.OfType<ImportDeclaration>())
                    {
                        if(resolvedImports==null ||
                           !resolvedImports.TryGetValue(source.File,out var imports) ||
                           !imports.TryGetValue(import.ModulePath.Start,out var targetFile))
                            throw new CompilationFailure(Diagnostic.Error(source.File,import.ModulePath,"TSCS1001",
                                "Module imports require a host-resolved source graph."));
                        if(!modules.TryGetValue(targetFile,out var target))
                            throw new CompilationFailure(Diagnostic.Error(source.File,import.ModulePath,"TSCS1001",
                                "The referenced source is not an ES module."));
                        var targetSource=moduleSources.First(p=>comparer.Equals(p.File,targetFile));
                        foreach(var specifier in import.Specifiers)
                        {
                            if(!targetSource.Root.Statements.OfType<Variable>().Any(
                                   v=>v.Exported && v.Name.Text==specifier.Imported.Text))
                                throw new CompilationFailure(Diagnostic.Error(source.File,specifier.Imported,"TS2305",
                                    $"Module '{import.ModulePath.Value}' has no exported member '{specifier.Imported.Text}'."));
                            Binder.Import(source.File,modules[source.File],specifier,target.Symbols[specifier.Imported.Text]);
                        }
                    }
                }
            }
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
                var checker=new TypeChecker(source.File);
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
    private static Diagnostic[] Order(IEnumerable<Diagnostic> diagnostics)=>
        diagnostics.OrderBy(d=>d.File,StringComparer.Ordinal).ThenBy(d=>d.Start)
            .ThenBy(d=>d.Length).ThenBy(d=>d.Code,StringComparer.Ordinal).ToArray();
}
