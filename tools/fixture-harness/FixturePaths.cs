using System.Text;
namespace Tscs.Conformance;
// Port of the pinned tspath byte/root and lexical resolution rules. Paths are
// projected one byte per char internally; malformed UTF8 is never replaced.
public static class FixturePaths
{
 private static string Text(byte[] p)=>Encoding.Latin1.GetString(p);
 private static byte[] Bytes(string p)=>Encoding.Latin1.GetBytes(p);
 private static bool Volume(char c)=>c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
 private static bool Fold(string p,string ascii)
 {
  var bytes=Bytes(p);int i=0,j=0;
  while(i<bytes.Length&&j<ascii.Length){if(Rune.DecodeFromUtf8(bytes.AsSpan(i),out var r,out int n)!=System.Buffers.OperationStatus.Done)return false;int c=r.Value;if(c is >=65 and <=90)c+=32;else if(c==0x17f)c='s';else if(c==0x212a)c='k';if(c!=ascii[j++])return false;i+=n;}
  return i==bytes.Length&&j==ascii.Length;
 }
 private static int Encoded(string p)
 {
  int n=p.Length;if(n==0)return 0;char c=p[0];
  if(c is '/' or '\\'){if(n==1||p[1]!=c)return 1;int end=p.IndexOf(c,2);return end<0?n:end+1;}
  if(Volume(c)&&n>1&&p[1]==':'){if(n==2)return 2;if(p[2] is '/' or '\\')return 3;}
  if(p.StartsWith("^/",StringComparison.Ordinal))
  {
   const string prefix="^/~ts-uri~/";
   if(p.StartsWith(prefix,StringComparison.Ordinal)){int s=p.IndexOf('/',prefix.Length);if(s>=0){int a=p.IndexOf('/',s+1);return a<0?~n:a+1;}}
   return 2;
  }
  int scheme=p.IndexOf("://",StringComparison.Ordinal);if(scheme<0)return 0;int start=scheme+3,endAuthority=p.IndexOf('/',start);if(endAuthority<0)return ~n;
  if(Fold(p[..scheme],"file")&&(endAuthority==start||Fold(p[start..endAuthority],"localhost"))&&n>endAuthority+2&&Volume(p[endAuthority+1]))
  {
   int end=endAuthority+2;
   if(p[end]==':')end++;else if(end+2<n&&p[end]=='%'&&p[end+1]=='3'&&(p[end+2] is 'a' or 'A'))end+=3;else end=-1;
   if(end==n)return ~end;if(end>=0&&p[end]=='/')return ~(end+1);
  }
  return ~(endAuthority+1);
 }
 private static int Root(string p){int n=Encoded(p);return n<0?~n:n;}
 private static string Slashes(string p)=>p.Replace('\\','/');
 private static bool Trailing(string p)=>p.Length>0&&(p[^1] is '/' or '\\');
 private static bool RelativeSegment(string p)=>p.Contains("//",StringComparison.Ordinal)||p.Split('/').Any(s=>s is "." or "..");
 private static string CombineText(string directory,string path)
 {
  directory=Slashes(directory);if(path.Length==0)return directory;path=Slashes(path);
  if(directory.Length==0||Root(path)!=0)return path;
  return directory+(directory.EndsWith('/')?"":"/")+path;
 }
 private static string AbsoluteNormalized(string p)
 {
  int root=Root(p);string simple=p;bool fast=!RelativeSegment(p);
  if(!fast)
  {
   string simplified=p.Replace("/./","/",StringComparison.Ordinal);
   string trimmed=simplified.StartsWith("./",StringComparison.Ordinal)?simplified[2..]:simplified;
   if(trimmed!=p&&!RelativeSegment(trimmed)&&!(trimmed!=simplified&&trimmed.StartsWith('/'))){simple=trimmed;fast=true;}
  }
  // Preserve the pinned whole-string fast path, including its behavior when
  // removing /./ changes a dynamic URI root. root is intentionally the old size.
  if(fast)
  {
   if(simple.Length>root)return Trailing(simple)?simple[..^1]:simple;
   if(simple.Length==root&&root!=0)return Trailing(simple)?simple:simple+"/";
   return simple;
  }
  var components=new List<string>();
  foreach(var segment in p[root..].Split('/'))
  {
   if(segment is "" or ".")continue;
   if(segment=="..")
   {
    if(components.Count>0&&components[^1]!=".."){components.RemoveAt(components.Count-1);continue;}
    if(components.Count==0&&root>0)continue;
   }
   components.Add(segment);
  }
  string prefix=p[..root];if(prefix.Length>0&&!Trailing(prefix))prefix+="/";
  return prefix+string.Join('/',components);
 }
 private static bool UrlSuffix(string p)=>Encoded(p)<0&&p.Contains("://",StringComparison.Ordinal)&&p[(p.IndexOf("://",StringComparison.Ordinal)+3)..].IndexOfAny(['?','#'])>=0;
 public static int EncodedRootLength(byte[] path)=>Encoded(Text(path));
 public static byte[] Normalize(byte[] path)
 {
  string p=Slashes(Text(path)),result=AbsoluteNormalized(p);
  if(result.Length>0&&p.EndsWith('/')&&!result.EndsWith('/'))result+="/";
  return Bytes(result);
 }
 public static byte[] Combine(byte[] directory,byte[] path)=>Bytes(CombineText(Text(directory),Text(path)));
 public static byte[] ToRooted(byte[] path,byte[] directory)
 {
  string p=Text(path),d=Text(directory);
  if(p.Length==0)throw new InvalidOperationException("path must not be empty");
  if(UrlSuffix(p))throw new InvalidOperationException("path must not contain a URL query or fragment");
  if(Encoded(p)==0&&Encoded(d)<0&&d.Contains("://",StringComparison.Ordinal)&&p.IndexOfAny(['?','#'])>=0)throw new InvalidOperationException("relative URL path must not contain a query or fragment");
  string result=AbsoluteNormalized(Encoded(p)==0&&d.Length>0?d+(Trailing(d)?"":"/")+Slashes(p):Slashes(p));
  if(Encoded(result)==0||UrlSuffix(result))throw new InvalidOperationException("path must be rooted");
  if(Root(result)==result.Length&&!Trailing(result))result+="/";
  return Bytes(result);
 }
}
