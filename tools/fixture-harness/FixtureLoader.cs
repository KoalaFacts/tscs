using System.Text;
namespace Tscs.Conformance;

public sealed record FixtureUnit(byte[] Name,byte[] Content,IReadOnlyDictionary<string,byte[]> Options);
public sealed record FixtureLink(byte[] Alias,byte[] Target);
public sealed record FixtureCallbackResult<T>(T Unit,Exception? Error);
// Units preserves Go nil when the first boundary callback fails. An EOF error
// still appends its callback value, which can itself be null.
public sealed record FixtureParseResult<T>(IReadOnlyList<T>? Units,IReadOnlyList<FixtureLink> Links,byte[] Directory,IReadOnlyDictionary<string,byte[]> Options,Exception? Error);

// ParseTestFilesAndSymlinksWithOptions. Raw units/callbacks only: no AST or VFS.
public static class FixtureLoader
{
    public static FixtureParseResult<T> Parse<T>(ReadOnlySpan<byte> source,byte[] fileName,Func<byte[],byte[],Dictionary<string,byte[]>,FixtureCallbackResult<T>> parseFile,bool implicitFirstFile=false)
    {
        var units=new List<T>();var links=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        var globals=new Dictionary<string,byte[]>(StringComparer.Ordinal);var options=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        using var body=new MemoryStream();byte[] name=implicitFirstFile?fileName.ToArray():[],directory=[];
        bool seenContentLine=false,hasSeenFile=false;Exception? error=null;
        bool Save(bool eof)
        {
            var result=parseFile(name.ToArray(),body.ToArray(),options);
            if(result.Error is null||eof)units.Add(result.Unit);
            error=result.Error;return error is null;
        }
        foreach(var line in FixtureDirectives.Read(source).Lines)
        {
            if(line.Directive is { } metadata)
            {
                if(metadata.LinkTarget is { } target){links[Encoding.Latin1.GetString(metadata.Value)]=target;continue;}
                string key=metadata.Name;byte[] value=metadata.Value;
                if(key=="currentdirectory")directory=value;
                if(key!="filename")
                {
                    if(key=="symlink"&&name.Length>0)
                    {
                        foreach(string part in Encoding.Latin1.GetString(value).Split(','))
                        {var alias=FixtureDirectives.TrimSpace(part);if(alias.Length>0)links[Encoding.Latin1.GetString(alias)]=name;}
                    }
                    else if(key is "emitthisfile" or "noopen")options[key]=value;
                    else globals[key]=value;
                    continue;
                }
                if(name.Length>0)
                {
                    if(!implicitFirstFile||body.Length!=0||hasSeenFile){hasSeenFile=true;if(!Save(false))break;}
                }
                else
                {
                    bool hasCode=body.Length!=0&&!FixtureTrivia.OnlyTrivia(body.ToArray());
                    if(hasCode&&!implicitFirstFile)throw new InvalidOperationException("Non-comment test content appears before the first '// @Filename' directive");
                }
                body.SetLength(0);seenContentLine=false;name=value;options=new(StringComparer.Ordinal);
            }
            else
            {
                if(implicitFirstFile?seenContentLine:body.Length!=0)body.WriteByte(10);
                seenContentLine=true;body.Write(line.Bytes);
            }
        }
        if(units.Count==0&&name.Length==0)name=BaseName(fileName);
        if(error is null)Save(true);
        return new(units.Count==0?null:units,links.Select(p=>new FixtureLink(Encoding.Latin1.GetBytes(p.Key),p.Value.ToArray())).ToArray(),directory,globals,error);
    }

    // The pinned tspath basename rule, independent of the executing host OS.
    private static byte[] BaseName(byte[] bytes)
    {
        string p=Encoding.Latin1.GetString(bytes).Replace('\\','/');int root=Root(p);
        if(root==p.Length)return [];
        if(p.EndsWith('/'))p=p[..^1];
        return Encoding.Latin1.GetBytes(p[Math.Max(root,p.LastIndexOf('/')+1)..]);
    }
    // Go EqualFold against ASCII words, retaining byte indices in the path.
    // The only non-ASCII simple-fold partners of ASCII are long s and Kelvin K.
    private static bool EqualsAsciiFold(string projected,string ascii)
    {
        byte[] bytes=Encoding.Latin1.GetBytes(projected);int p=0,i=0;
        while(p<bytes.Length&&i<ascii.Length)
        {
            if(Rune.DecodeFromUtf8(bytes.AsSpan(p),out var rune,out int size)!=System.Buffers.OperationStatus.Done)return false;
            int c=rune.Value;if(c is >=65 and <=90)c+=32;else if(c==0x17f)c='s';else if(c==0x212a)c='k';
            if(c!=ascii[i++])return false;p+=size;
        }
        return p==bytes.Length&&i==ascii.Length;
    }
    private static bool Volume(char c)=>c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    private static int Root(string p)
    {
        int n=p.Length;if(n==0)return 0;
        if(p[0]=='/'){if(n==1||p[1]!='/')return 1;int i=p.IndexOf('/',2);return i<0?n:i+1;}
        if(Volume(p[0])&&n>1&&p[1]==':'){if(n==2)return 2;if(p[2]=='/')return 3;}
        if(p.StartsWith("^/",StringComparison.Ordinal))
        {
            const string prefix="^/~ts-uri~/";
            if(p.StartsWith(prefix,StringComparison.Ordinal))
            {int scheme=p.IndexOf('/',prefix.Length);if(scheme>=0){int end=p.IndexOf('/',scheme+1);return end<0?n:end+1;}}
            return 2;
        }
        int s=p.IndexOf("://",StringComparison.Ordinal);if(s<0)return 0;
        int authorityStart=s+3,authorityEnd=p.IndexOf('/',authorityStart);if(authorityEnd<0)return n;
        if(EqualsAsciiFold(p[..s],"file")&&(authorityEnd==authorityStart||EqualsAsciiFold(p[authorityStart..authorityEnd],"localhost"))&&n>authorityEnd+2&&Volume(p[authorityEnd+1]))
        {
            int end=authorityEnd+2;
            if(p[end]==':')end++;
            else if(end+2<n&&p[end]=='%'&&p[end+1]=='3'&&(p[end+2] is 'a' or 'A'))end+=3;
            else end=-1;
            if(end==n)return end;if(end>=0&&p[end]=='/')return end+1;
        }
        return authorityEnd+1;
    }
}
