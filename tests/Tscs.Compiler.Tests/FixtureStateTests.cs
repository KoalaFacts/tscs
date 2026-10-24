using System.Text.Json;
using System.Text.Json.Nodes;
using Tscs.Conformance;
using Xunit;
namespace Tscs.Tests;
public class FixtureStateTests
{
    public static IEnumerable<object[]> Vectors()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/fixture-state.json")));
        foreach(var row in data.RootElement.GetProperty("vectors").EnumerateArray())yield return new object[]{row.GetProperty("id").GetString()!,row.GetRawText()};
    }
    [Fact]
    public void StreamContinuesAfterPreludePanicAndPreservesNullCallbackValue()
    {
        const string requests="{\"id\":\"prelude\",\"input\":\"eDsKLy8gQGZpbGVuYW1lOiBhLnRz\",\"fileName\":\"YS50cw==\",\"implicit\":false,\"failAt\":0,\"returnNil\":false}\n{\"id\":\"nil\",\"input\":\"eDs=\",\"fileName\":\"YS50cw==\",\"implicit\":false,\"failAt\":1,\"returnNil\":true}\n";
        using var output=new StringWriter();FixtureProtocol.Run(new StringReader(requests),output);
        var rows=output.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>JsonNode.Parse(x)!).ToArray();
        Assert.Equal(2,rows.Length);Assert.Equal("prelude",rows[0]["id"]!.GetValue<string>());
        Assert.Equal("Non-comment test content appears before the first '// @Filename' directive",rows[0]["expected"]!["panic"]!.GetValue<string>());
        Assert.Equal(0,rows[0]["expected"]!["calls"]!.GetValue<int>());
        Assert.Equal("nil",rows[1]["id"]!.GetValue<string>());Assert.Equal("callback",rows[1]["expected"]!["error"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("[null]"),rows[1]["expected"]!["units"]));
    }
    [Fact]
    public void MalformedProtocolInputFailsInsteadOfLosingARequest()
    {
        using var output=new StringWriter();
        Assert.ThrowsAny<JsonException>(()=>FixtureProtocol.Run(new StringReader("{bad json"),output));
        Assert.Equal("",output.ToString());
    }
    [Theory,MemberData(nameof(Vectors))]
    public void StateAndCallbackResultsMatchPinnedGo(string id,string json)
    {
        Assert.NotEmpty(id);using var row=JsonDocument.Parse(json);var r=row.RootElement;
        object actual=FixtureProtocol.Probe(r);
        string text=JsonSerializer.Serialize(actual,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(r.GetProperty("expected").GetRawText()),JsonNode.Parse(text)),id+"\n"+text);
    }
}
