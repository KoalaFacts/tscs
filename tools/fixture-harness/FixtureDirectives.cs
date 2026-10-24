using System.Text;
using System.Text.RegularExpressions;
using Tscs.Compiler;

namespace Tscs.Conformance;

public sealed record FixtureDirective(string Name,byte[] Value,byte[]? LinkTarget);
public sealed record FixtureLine(int ByteOffset,byte[] Bytes,FixtureDirective? Directive);
public sealed record FixtureSource(byte[] Bytes,IReadOnlyList<FixtureLine> Lines);

// A byte-preserving lexical boundary for the pinned harness. This does NOT yet
// apply file/global scope, build virtual files or validate discarded preludes.
public static class FixtureDirectives
{
    // Go regexp is non-backtracking; avoid polynomial retries on arrowless links.
    private static readonly Regex Option=new(@"^//[\t\n\f\r ]*@([A-Za-z0-9_]+)[\t\n\f\r ]*:[\t\n\f\r ]*([^\r\n]*)",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
    private static readonly Regex Link=new(@"^//[\t\n\f\r ]*@link[\t\n\f\r ]*:[\t\n\f\r ]*([^\r\n]*)[\t\n\f\r ]*->[\t\n\f\r ]*([^\r\n]*)",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);

    public static FixtureSource Read(ReadOnlySpan<byte> source)
    {
        byte[] bytes=SourceDecoder.Decode(source);var lines=new List<FixtureLine>();int start=0;
        for(int pos=0;pos<=bytes.Length;pos++)
        {
            if(pos<bytes.Length&&bytes[pos]!=10)continue;
            int end=pos;
            if(pos<bytes.Length&&end>start&&bytes[end-1]==13)end--;
            byte[] line=bytes.AsSpan(start,end-start).ToArray();
            lines.Add(new(start,line,ParseLine(line)));start=pos+1;
        }
        return new(bytes,lines);
    }

    private static FixtureDirective? ParseLine(byte[] bytes)
    {
        // The pinned Go regex grammar is ASCII. Latin1 projects one byte to one
        // code unit for matching; captures are restored to UTF8 bytes immediately.
        string line=Encoding.Latin1.GetString(bytes);var link=Link.Match(line);
        if(link.Success)return new("link",TrimSpace(link.Groups[2].Value),TrimSpace(link.Groups[1].Value));
        var option=Option.Match(line);
        return option.Success?new(option.Groups[1].Value.ToLowerInvariant(),TrimSpace(option.Groups[2].Value),null):null;
    }

    private static byte[] TrimSpace(string projected)
    {
        byte[] bytes=Encoding.Latin1.GetBytes(projected);int start=0,end=bytes.Length;
        while(start<end)
        {
            if(Rune.DecodeFromUtf8(bytes.AsSpan(start,end-start),out var rune,out int size)!=System.Buffers.OperationStatus.Done||!IsGoSpace(rune.Value))break;
            start+=size;
        }
        while(end>start)
        {
            if(Rune.DecodeLastFromUtf8(bytes.AsSpan(start,end-start),out var rune,out int size)!=System.Buffers.OperationStatus.Done||!IsGoSpace(rune.Value))break;
            end-=size;
        }
        return bytes.AsSpan(start,end-start).ToArray();
    }
    // strings.TrimSpace uses unicode.IsSpace, not scanner whitespace. In
    // particular U+200B and U+FEFF must remain; malformed byte 0x85 is not U+0085.
    private static bool IsGoSpace(int c)=>c is >=9 and <=13 or 0x20 or 0x85 or 0xa0 or 0x1680 or >=0x2000 and <=0x200a or 0x2028 or 0x2029 or 0x202f or 0x205f or 0x3000;
}
