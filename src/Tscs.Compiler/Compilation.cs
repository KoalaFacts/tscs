namespace Tscs;

public sealed record CompilationAnalysis(IReadOnlyList<Diagnostic> Diagnostics, bool HasSyntaxErrors);

public static class Compilation
{
    public static IReadOnlyList<Diagnostic> Check(string file,string text)=>Analyze(file,text).Diagnostics;

    public static CompilationAnalysis Analyze(string file,string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        bool syntaxPhase=true;
        Scanner? scanner=null;
        try
        {
            if(text.Length>1_000_000)throw new CompilationFailure(Diagnostic.Error(file,new(TokenKind.End,"",0,0),"TSCS1001","Source exceeds the bootstrap size limit."));
            scanner=new Scanner(file,text);
            var tokens=scanner.Scan();
            if(scanner.Diagnostics.Count>0)return new(Order(scanner.Diagnostics),true);
            var parser=new Parser(file,tokens);
            var block=parser.Parse();
            if(parser.Diagnostics.Count>0)return new(Order(parser.Diagnostics),true);
            syntaxPhase=false;
            return new(Order(new TypeChecker(file).Check(block).Where(d=>!scanner.SuppressesSemanticDiagnostic(d.Start))),false);
        }
        catch(CompilationFailure failure)
        {
            var lexical=scanner?.Diagnostics ?? [];
            return new(Order(lexical.Append(failure.Diagnostic)),lexical.Count>0 || (syntaxPhase && !failure.Diagnostic.Code.StartsWith("TSCS",StringComparison.Ordinal)));
        }
    }
    private static Diagnostic[] Order(IEnumerable<Diagnostic> diagnostics)=>diagnostics.OrderBy(d=>d.Start).ThenBy(d=>d.Length).ThenBy(d=>d.Code,StringComparer.Ordinal).ToArray();
}
