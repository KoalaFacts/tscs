namespace Tscs;

// Pinned tsoptions lexical path conversion, with no host filesystem access.
internal static class JsonConfigPaths
{
 internal static JsonScalarOptionResult Convert(JsonScalarOption option,int pathKind,
  ConfigValue value,string basePath,string file="",Token? name=null,Token? expression=null)
 {
  var scalar=JsonScalarOptions.Convert(option,value,file,name,expression);
  if(scalar.Diagnostics.Count>0||scalar.Value.Kind!="String"||!Rooted(pathKind))return scalar;
  string raw=scalar.Value.Text;
  string absolute=NormalizedAbsolute(raw,basePath);
  if(NetworkPath(absolute))
  {
   const string code="TS5116";
   // Pinned diagnostics.Format (strings.ToValidUTF8) replaces each contiguous
   // malformed WTF-8 run once; keep raw path for validation and the caller.
   string message=$"Network path '{JsonOptionsFromJson.DiagnosticArgument(raw)}' is not supported.";
   var diagnostic=file.Length>0&&expression is Token location
    ?Diagnostic.Error(file,location,code,message)
    :new Diagnostic(code,"error","",-1,0,message,[],[]);
   return new(new("Null","",0,false,[],[]),[diagnostic]);
  }
  // Preserve the special configDir template after checking the resolved path.
  string normalized=PinnedEnumLowercase.Lower(raw).StartsWith("${configdir}",StringComparison.Ordinal)
   ?Slashes(raw):absolute;
  if(normalized.Length==0)normalized=".";
  return new(scalar.Value with {Text=normalized},scalar.Diagnostics);
 }
 internal static bool Rooted(int kind)=>kind is 1 or 2 or 3 or 7;
 private static bool Separator(char c)=>c is '/' or '\\';
 private static bool Volume(char c)=>c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
 private static string Slashes(string s)=>s.Replace('\\','/');
 // Go strings.EqualFold uses Unicode simple case folding, not
 // OrdinalIgnoreCase. Its non-ASCII equivalents of ASCII letters are
 // U+017F (long s) and U+212A (Kelvin sign).
 private static bool GoEqualFoldAscii(string value,string ascii)
 {
  if(value.Length!=ascii.Length)return false;
  for(int i=0;i<ascii.Length;i++)
  {
   char ch=value[i];
   if(ch=='\u017F')ch='s';
   else if(ch=='\u212A')ch='k';
   else if(ch is >= 'A' and <= 'Z')ch=(char)(ch+('a'-'A'));
   if(ch!=ascii[i])return false;
  }
  return true;
 }
 private static bool NetworkPath(string s)=>s.Length>=2&&Separator(s[0])
  &&(Separator(s[1])||s.Length>=3&&s[1]=='?'&&s[2]=='?'
      &&(s.Length==3||Separator(s[3])));
 private static int RootLength(string s)
 {
  int n=s.Length;if(n==0)return 0;char c=s[0];
  if(Separator(c)){if(n==1||s[1]!=c)return 1;int end=s.IndexOf(c,2);return end<0?n:end+1;}
  if(Volume(c)&&n>1&&s[1]==':'){if(n==2)return 2;if(Separator(s[2]))return 3;}
  if(s.StartsWith("^/",StringComparison.Ordinal))
  {
   const string prefix="^/~ts-uri~/";
   if(s.StartsWith(prefix,StringComparison.Ordinal))
   {
    int schemeEnd=s.IndexOf('/',prefix.Length);
    if(schemeEnd>=0){int authorityEnd=s.IndexOf('/',schemeEnd+1);return authorityEnd<0?n:authorityEnd+1;}
   }
   return 2;
  }
  int scheme=s.IndexOf("://",StringComparison.Ordinal);
  if(scheme<0)return 0;
  int begin=scheme+3,endAuthority=s.IndexOf('/',begin);
  if(endAuthority<0)return n;
  if(GoEqualFoldAscii(s[..scheme],"file")
   &&(endAuthority==begin||GoEqualFoldAscii(s[begin..endAuthority],"localhost"))
   &&n>endAuthority+2&&Volume(s[endAuthority+1]))
  {
   int end=endAuthority+2;
   if(s[end]==':')end++;
   else if(end+2<n&&s[end]=='%'&&s[end+1]=='3'&&(s[end+2] is 'a' or 'A'))end+=3;
   else end=-1;
   if(end==n)return end;
   if(end>=0&&s[end]=='/')return end+1;
  }
  return endAuthority+1;
 }
 private static string NormalizedAbsolute(string path,string basePath)
 {
  path=Slashes(path);
  if(RootLength(path)==0&&basePath.Length>0)
  {
   string directory=Slashes(basePath);
   path=directory+(directory.EndsWith('/')?"":"/")+path;
  }
  int rootLength=RootLength(path);string root=path[..rootLength];
  var parts=new List<string>();
  foreach(string item in path[rootLength..].Split('/'))
  {
   if(item is "" or ".")continue;
   if(item=="..")
   {
    if(parts.Count>0&&parts[^1]!=".."){parts.RemoveAt(parts.Count-1);continue;}
    if(rootLength>0)continue;
   }
   parts.Add(item);
  }
  if(parts.Count==0)return rootLength>0?(root.EndsWith('/')?root:root+"/"):"";
  return root+(root.Length>0&&!root.EndsWith('/')?"/":"")+string.Join('/',parts);
 }
}
