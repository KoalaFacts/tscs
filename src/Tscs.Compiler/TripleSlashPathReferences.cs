using System.Text.RegularExpressions;

namespace Tscs;

internal readonly record struct TripleSlashPathReference(int Start,string Path);

/// <summary>
/// Recognizes a deliberately bounded set of leading TypeScript path references.
/// The same scanner is used when building the source graph and when admitting
/// directive comments into the compiler token stream.
/// </summary>
internal static class TripleSlashPathReferences
{
    private static readonly Regex DirectivePattern=new(
        """^///[ \t]*<reference[ \t]+path[ \t]*=[ \t]*(?:"(?<double>[^"]+)"|'(?<single>[^']+)')[ \t]*/>[ \t]*$""",
        RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));

    public static IReadOnlyList<TripleSlashPathReference> Collect(string text)
    {
        var references=new List<TripleSlashPathReference>();
        int offset=0;
        if(text.Length>0 && text[0]=='\uFEFF')offset++;
        while(offset<text.Length)
        {
            while(offset<text.Length && (char.IsWhiteSpace(text[offset]) || text[offset]=='\uFEFF'))offset++;
            if(offset+1>=text.Length || text[offset]!='/')break;
            if(text[offset+1]=='/')
            {
                int start=offset;
                while(offset<text.Length && !Scanner.IsLineBreak(text[offset]))offset++;
                string comment=text[start..offset];
                if(comment.StartsWith("///",StringComparison.Ordinal))
                {
                    if(!TryRead(comment,out string path))break;
                    references.Add(new(start,path));
                }
                continue;
            }
            if(text[offset+1]=='*')
            {
                int end=text.IndexOf("*/",offset+2,StringComparison.Ordinal);
                if(end<0)break;
                offset=end+2;
                continue;
            }
            break;
        }
        return references;
    }

    private static bool TryRead(string comment,out string path)
    {
        path="";
        var match=DirectivePattern.Match(comment);
        if(!match.Success)return false;
        path=match.Groups["double"].Success
            ? match.Groups["double"].Value
            : match.Groups["single"].Value;
        // XML entities, URI schemes and extra reference attributes are not
        // yet modeled. Reject rather than resolving a different source.
        return !string.IsNullOrWhiteSpace(path) && !path.Contains('&') && !path.Contains('\0');
    }
}
