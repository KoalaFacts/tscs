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
    [Theory,MemberData(nameof(Vectors))]
    public void StateAndCallbackResultsMatchPinnedGo(string id,string json)
    {
        Assert.NotEmpty(id);using var row=JsonDocument.Parse(json);var r=row.RootElement;
        int calls=0;object actual;
        try
        {
            var result=FixtureLoader.Parse(Convert.FromBase64String(r.GetProperty("input").GetString()!),Convert.FromBase64String(r.GetProperty("fileName").GetString()!),
                (name,body,options)=>{calls++;return new FixtureCallbackResult<FixtureUnit?>(r.GetProperty("returnNil").GetBoolean()?null:new FixtureUnit(name,body,new Dictionary<string,byte[]>(options)),calls==r.GetProperty("failAt").GetInt32()?new Exception("callback"):null);},r.GetProperty("implicit").GetBoolean());
            var links=result.Links.OrderBy(x=>Convert.ToHexString(x.Alias),StringComparer.Ordinal).ToArray();
            actual=new{units=result.Units,links,directory=result.Directory,options=result.Options,error=result.Error?.Message,panic=(string?)null,calls};
        }
        catch(InvalidOperationException e){actual=new{panic=e.Message,calls};}
        string text=JsonSerializer.Serialize(actual,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(r.GetProperty("expected").GetRawText()),JsonNode.Parse(text)),id+"\n"+text);
    }
}
