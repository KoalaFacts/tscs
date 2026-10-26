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
    public static CompilationAnalysis AnalyzeFiles(IReadOnlyList<SourceInput> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if(files.Count==0)throw new ArgumentException("At least one source file is required.",nameof(files));
        if(files.Select(f=>f.File).Distinct(StringComparer.Ordinal).Count()!=files.Count)
            throw new ArgumentException("Duplicate source paths are not supported.",nameof(files));

        var parsed=new List<ParsedSource>(files.Count);
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
                if(scanner.Diagnostics.Count>0)return new(Order(scanner.Diagnostics),true);
                var parser=new Parser(source.File,tokens);
                var block=parser.Parse();
                if(parser.Diagnostics.Count>0)return new(Order(parser.Diagnostics),true);
                syntaxPhase=false;
                parsed.Add(new(source.File,block,scanner));
            }
            catch(CompilationFailure failure)
            {
                var lexical=scanner?.Diagnostics ?? [];
                return new(Order(lexical.Append(failure.Diagnostic)),
                    lexical.Count>0 || (syntaxPhase && !failure.Diagnostic.Code.StartsWith("TSCS",StringComparison.Ordinal)));
            }
        }
        Scope? global=null;
        try
        {
            if(parsed.Count>1)global=Binder.BindProgram(parsed.Select(p=>(p.File,p.Root)));
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
                var checkedDiagnostics=global==null
                    ? checker.Check(source.Root)
                    : checker.CheckBound(source.Root,global);
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
        diagnostics.OrderBy(d=>d.Start).ThenBy(d=>d.Length).ThenBy(d=>d.Code,StringComparer.Ordinal).ToArray();
}
