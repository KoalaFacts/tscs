namespace Tscs;

public sealed record CompilationAnalysis(IReadOnlyList<Diagnostic> Diagnostics, bool HasSyntaxErrors);

public static class Compilation
{
    public static IReadOnlyList<Diagnostic> Check(string file,string text)=>Analyze(file,text).Diagnostics;

    public static CompilationAnalysis Analyze(string file,string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        bool syntaxPhase=true;
        try
        {
            if(text.Length>1_000_000)throw new CompilationFailure(Diagnostic.Error(file,new(TokenKind.End,"",0,0),"TSCS1001","Source exceeds the bootstrap size limit."));
            var parser=new Parser(file,new Scanner(file,text).Scan());
            var block=parser.Parse();
            if(parser.Diagnostics.Count>0)return new(Order(parser.Diagnostics),true);
            syntaxPhase=false;
            return new(Order(new TypeChecker(file).Check(block)),false);
        }
        catch(CompilationFailure failure){return new([failure.Diagnostic],syntaxPhase && !failure.Diagnostic.Code.StartsWith("TSCS",StringComparison.Ordinal));}
    }
    private static Diagnostic[] Order(IEnumerable<Diagnostic> diagnostics)=>diagnostics.OrderBy(d=>d.Start).ThenBy(d=>d.Length).ThenBy(d=>d.Code,StringComparer.Ordinal).ToArray();
}
