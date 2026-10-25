using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureVfsTests
{
 public static IEnumerable<object[]> EntriesVectors(){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/vfs-entries.json")));foreach(var r in doc.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Theory,MemberData(nameof(EntriesVectors))]
 public void AccessibleEntriesMatchPinnedGo(string id,string json){Assert.NotEmpty(id);using var doc=JsonDocument.Parse(json);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(doc.RootElement.GetProperty("expected").GetRawText()),JsonSerializer.SerializeToNode(FixtureVfs.ProbeEntries(doc.RootElement))),id);}
 public static IEnumerable<object[]> Vectors(){using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/vfs.json")));foreach(var r in doc.RootElement.GetProperty("vectors").EnumerateArray())yield return [r.GetProperty("id").GetString()!,r.GetRawText()];}
 [Fact]
 public void CyclesRemainExplicitlyUnsupportedInsteadOfMissingFiles()
 {
  using var doc=JsonDocument.Parse("{\"entries\":[{\"path\":\"L2E=\",\"data\":\"L2I=\",\"link\":true},{\"path\":\"L2I=\",\"data\":\"L2E=\",\"link\":true}],\"queries\":[\"L2E=\"]}");
  Assert.Throws<NotSupportedException>(()=>FixtureVfs.Probe(doc.RootElement));
 }
 [Fact]
 public void ExpandingSymlinkCyclesRemainExplicitlyUnsupported()
 {
  using var doc=JsonDocument.Parse("{\"entries\":[{\"path\":\"L2E=\",\"data\":\"L2Evc3Vi\",\"link\":true}],\"queries\":[\"L2EveA==\"]}");
  Assert.Throws<NotSupportedException>(()=>FixtureVfs.Probe(doc.RootElement));
 }
 [Theory,MemberData(nameof(Vectors))]
 public void ReadOnlyVfsMatchesPinnedGo(string id,string json){Assert.NotEmpty(id);using var doc=JsonDocument.Parse(json);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(doc.RootElement.GetProperty("expected").GetRawText()),JsonSerializer.SerializeToNode(FixtureVfs.Probe(doc.RootElement))),id);}
}
