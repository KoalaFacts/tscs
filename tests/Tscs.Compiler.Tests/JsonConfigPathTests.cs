using Xunit;
namespace Tscs.Tests;

// Pinned TS7 tsconfigparsing.normalizeNonListOptionValue,
// isNetworkPathValue and convertJsonOption boundary regressions.
// The compiler does not read or create host files for these lexical values.
public class JsonConfigPathTests
{
 private static ConfigValue StringValue(string value)=>new("String",value,0,false,[],[]);
 private static JsonOptionsMapResult One(string name,ConfigValue value,string root="/.src")
  =>JsonOptionsFromJson.Convert(new("Object","",0,false,[new(name,value)],[]),root);

 [Theory]
 [InlineData("outDir","foo","/.src","/.src/foo")]
 [InlineData("outDir","../dist","/.src","/dist")]
 [InlineData("baseUrl",".","/app/src","/app/src")]
 [InlineData("outFile",@"C:\build\..\x.js","/.src","C:/x.js")]
 [InlineData("rootDir",@"..\types","/app/src","/app/types")]
 [InlineData("rootDir","${configDir}/src","/app/src","${configDir}/src")]
 [InlineData("sourceRoot","https://server/root","/.src","https://server/root")]
 [InlineData("mapRoot",@"C:\url\..\x","/.src",@"C:\url\..\x")]
 public void PinnedRootedAndSourceMapPathKindsRemainDistinct(string option,string raw,string root,string expected)
 {
  var result=One(option,StringValue(raw),root);
  Assert.Empty(result.Unsupported);
  Assert.Empty(result.Diagnostics);
  var call=Assert.Single(result.Calls);
  Assert.Equal(option,call.Key);
  Assert.Equal("String",call.Value.Kind);
  Assert.Equal(expected,call.Value.Text);
 }

 [Theory]
 [InlineData("outDir","//server/share")]
 [InlineData("outDir",@"\\server\share")]
 [InlineData("rootDir","/??/UNC/server/share")]
 public void ResolvedNetworkAndDevicePathsRejectWithoutHostAccess(string option,string path)
 {
  var result=One(option,StringValue(path));
  Assert.Empty(result.Unsupported);
  var call=Assert.Single(result.Calls);
  Assert.Equal("Null",call.Value.Kind);
  var diagnostic=Assert.Single(result.Diagnostics);
  Assert.Equal("TS5116",diagnostic.Code);
  Assert.Equal("Network path '"+path+"' is not supported.",diagnostic.Message);
  Assert.Equal(-1,diagnostic.Start);
  Assert.Equal(0,diagnostic.Length);
 }

 [Fact]
 public void RootDirsNormalizeEachListElementRelativeToConfigDirectory()
 {
  var values=new ConfigValue("Array","",0,false,[],[StringValue("../shared"),StringValue("src")]);
  var result=One("rootDirs",values,"/app/src");
  Assert.Empty(result.Unsupported);
  Assert.Empty(result.Diagnostics);
  var call=Assert.Single(result.Calls);
  Assert.Equal("Array",call.Value.Kind);
  Assert.Equal(["/app/shared","/app/src/src"],call.Value.Elements.Select(x=>x.Text).ToArray());
 }

 [Fact]
 public void LocaleCommandLineOnlyDiagnosticPrecedesExtraLocaleValidation()
 {
  var result=One("locale",StringValue("not_a_locale"));
  Assert.Empty(result.Unsupported);
  Assert.Equal("Null",Assert.Single(result.Calls).Value.Kind);
  var diagnostic=Assert.Single(result.Diagnostics);
  Assert.Equal("TS6266",diagnostic.Code);
  Assert.Equal("Option 'locale' can only be specified on command line.",diagnostic.Message);
 }
}
