using System.Text;
using System.Text.Json;
using Tscs.Compiler;
namespace Tscs.Conformance;
// Read-only, case-sensitive snapshot of vfstest.MapFS. No host IO, mutation,
// directory enumeration or claim of complete VFS support.
public static class FixtureVfs
{
 private sealed record Node(string Path,byte[] Data,bool Directory,string? Target=null);
 private sealed class Snapshot
 {
  private readonly Dictionary<string,Node> nodes=new(StringComparer.Ordinal);
  private static string Text(byte[] p)=>Encoding.Latin1.GetString(p);
  private static byte[] Bytes(string p)=>Encoding.Latin1.GetBytes(p);
  private static string Quote(string p)=>JsonSerializer.Serialize(p,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
  private static string Strip(string p)=>p.StartsWith('/')?p[1..]:p;
  private static int CompareParts(string a,string b)
  {
   while(true){int ai=a.IndexOf('/'),bi=b.IndexOf('/');if(ai<0||bi<0)return StringComparer.Ordinal.Compare(a,b);int c=StringComparer.Ordinal.Compare(a[..ai],b[..bi]);if(c!=0)return c;a=a[(ai+1)..];b=b[(bi+1)..];}
  }
  public Snapshot(JsonElement entries)
  {
   var input=new Dictionary<string,Node>(StringComparer.Ordinal);bool posix=false,windows=false;
   foreach(var e in entries.EnumerateArray())
   {
    string p=Text(Convert.FromBase64String(e.GetProperty("path").GetString()!));byte[] data=Convert.FromBase64String(e.GetProperty("data").GetString()!);
    input[p]=new(p,data,false,e.GetProperty("link").GetBoolean()?Text(data):null);
   }
   void Check(string p)
   {
    if(FixturePaths.EncodedRootLength(Bytes(p))<=0)throw new InvalidOperationException("non-rooted path "+Quote(p));
    string normal=Text(FixturePaths.Normalize(Bytes(p)));if(normal.Length>0&&(normal[^1] is '/' or '\\'))normal=normal[..^1];
    if(normal!=p)throw new InvalidOperationException("non-normalized path "+Quote(p));
    if(p.StartsWith('/'))posix=true;else windows=true;
   }
   var sorted=input.Keys.OrderBy(p=>p,Comparer<string>.Create(CompareParts)).ToArray();
   foreach(string p in sorted){Check(p);if(input[p].Target is { } target)Check(target);}
   if(posix&&windows)throw new InvalidOperationException("mixed posix and windows paths");
   foreach(string source in sorted)
   {
    string p=Strip(source);int slash=p.LastIndexOf('/');
    if(slash>0)
    {
     try{EnsureDirectory(p[..slash]);}
     catch(InvalidOperationException e){throw new InvalidOperationException("failed to create intermediate directories for "+Quote(p)+": "+e.Message);}
    }
    var n=input[source];nodes[p]=new(p,n.Data,false,n.Target is null?null:Strip(n.Target));
   }
  }
  private Node? Resolve(string p,HashSet<string> active,out Node? broken)
  {
   if(active.Count>=256)throw new NotSupportedException("TSCS VFS: deep symlink resolution is not reference-verified");
   if(!active.Add(p))throw new NotSupportedException("TSCS VFS: cyclic symlinks are not reference-verified");
   try
   {
    if(nodes.TryGetValue(p,out var n))
    {
     if(n.Target is null){broken=null;return n;}
     var result=Resolve(n.Target,active,out var inner);broken=result is null?inner??n:null;return result;
    }
    var matches=nodes.Values.Where(n=>n.Target is not null&&p.Length>n.Path.Length&&p.StartsWith(n.Path+"/",StringComparison.Ordinal)).ToArray();
    if(matches.Length>1)throw new NotSupportedException("TSCS VFS: overlapping symlink prefixes require reference-order semantics");
    if(matches.Length==1){var link=matches[0];var result=Resolve(link.Target+p[link.Path.Length..],active,out var inner);broken=result is null?inner??link:null;return result;}
    broken=null;return null;
   }
   finally{active.Remove(p);}
  }
  private Node? Resolve(string p,out Node? broken)=>Resolve(p,new(StringComparer.Ordinal),out broken);
  private string EnsureDirectory(string p)
  {
   var existing=Resolve(p,out Node? broken);
   if(existing is not null){if(!existing.Directory)throw new InvalidOperationException("mkdir "+Quote(p)+": path exists but is not a directory");return existing.Path;}
   if(broken is not null&&nodes.TryGetValue(p,out var current)&&current.Target is not null)throw new InvalidOperationException("broken symlink "+Quote(broken.Path)+" -> "+Quote(broken.Target!));
   int slash=p.LastIndexOf('/');string actual=slash<0?p:EnsureDirectory(p[..slash])+p[slash..];
   nodes[actual]=new(actual,[],true);return actual;
  }
  private static bool Valid(string p)
  {
   byte[] bytes=Bytes(p);int i=0;while(i<bytes.Length){if(Rune.DecodeFromUtf8(bytes.AsSpan(i),out _,out int n)!=System.Buffers.OperationStatus.Done)return false;i+=n;}
   return p=="."||p.Length>0&&p.Split('/').All(s=>s is not "" and not "." and not "..");
  }
  // fstest.MapFS.Open performs a second, parent-first POSIX symlink pass.
  private string StandardResolve(string p,HashSet<string> active)
  {
   if(active.Count>=256)throw new NotSupportedException("TSCS VFS: deep standard symlink resolution is not reference-verified");
   if(!active.Add(p))throw new NotSupportedException("TSCS VFS: cyclic standard symlink resolution is not reference-verified");
   try
   {
    string Join(string a,string b){var parts=new List<string>();foreach(string x in (a+"/"+b).Split('/')){if(x is "" or ".")continue;if(x==".."&&parts.Count>0&&parts[^1]!="..")parts.RemoveAt(parts.Count-1);else parts.Add(x);}return parts.Count==0?".":string.Join('/',parts);}
    string Follow(string name,string target,string suffix){int slash=name.LastIndexOf('/');return StandardResolve(Join(slash<0?".":name[..slash],target)+suffix,active);}
    if(nodes.TryGetValue(p,out var direct)&&direct.Target is { } target)return Follow(p,target,"");
    for(int i=0;i<p.Length;){int slash=p.IndexOf('/',i);string prefix=slash<0?p:p[..slash];if(nodes.TryGetValue(prefix,out var n)&&n.Target is { } t)return Follow(prefix,t,slash<0?"":p[slash..]);if(slash<0)break;i=slash+1;}
    return p;
   }
   finally{active.Remove(p);}
  }
  public object Query(byte[] input)
  {
   if(FixturePaths.EncodedRootLength(input)==0)throw new InvalidOperationException("path must be absolute");
   byte[] path=FixturePaths.ToRooted(input,[]);string q=Text(path),p=Strip(q);
   var real=Resolve(p,out _);byte[] realpath=real is null?path:FixturePaths.ToRooted(Bytes((q.StartsWith('/')?"/":"")+real.Path),[]);
   // fs.Sub joins a drive root with '.', removing its final slash for Open.
   string open=p.Length==0?".":FixturePaths.EncodedRootLength(path)==path.Length?p.TrimEnd('/'):p;
   var first=Resolve(open,out _);open=StandardResolve(first?.Path??open,new(StringComparer.Ordinal));
   nodes.TryGetValue(open,out var n);bool valid=Valid(open),directory=valid&&(open=="."||n?.Directory==true),file=valid&&n is {Directory:false,Target:null};
   byte[] content=file?SourceDecoder.Decode(n!.Data):[];
   return new{path,content,readOk=file,fileExists=file,directoryExists=directory,realpath};
  }
 }
 public static object Probe(JsonElement request)
 {
  try{var fs=new Snapshot(request.GetProperty("entries"));return new{queries=request.GetProperty("queries").EnumerateArray().Select(q=>fs.Query(Convert.FromBase64String(q.GetString()!))).ToArray(),panic=(string?)null};}
  catch(InvalidOperationException e){return new{panic=e.Message};}
 }
}
