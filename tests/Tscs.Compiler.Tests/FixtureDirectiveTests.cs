using System.Text.Json;
using Tscs.Conformance;
using Xunit;

namespace Tscs.Tests;

public class FixtureDirectiveTests
{
    public static IEnumerable<object[]> Vectors()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/fixture-directives.json")));
        foreach(var row in data.RootElement.GetProperty("vectors").EnumerateArray())
            yield return new object[]{row.GetProperty("id").GetString()!,row.GetProperty("input").GetString()!,row.GetRawText()};
    }
    [Theory,MemberData(nameof(Vectors))]
    public void LexicalBytesAndDirectivesMatchPinnedGo(string id,string input,string expectedJson)
    {
        Assert.NotEmpty(id);using var expected=JsonDocument.Parse(expectedJson);
        var result=FixtureDirectives.Read(Convert.FromBase64String(input));
        Assert.Equal(Convert.FromBase64String(expected.RootElement.GetProperty("decoded").GetString()!),result.Bytes);
        var lines=expected.RootElement.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(lines.Length,result.Lines.Count);
        for(int i=0;i<lines.Length;i++)
        {
            var line=lines[i];var actual=result.Lines[i];
            Assert.Equal(line.GetProperty("offset").GetInt32(),actual.ByteOffset);
            Assert.Equal(Convert.FromBase64String(line.GetProperty("bytes").GetString()!),actual.Bytes);
            var directive=line.GetProperty("directive");
            if(directive.ValueKind==JsonValueKind.Null){Assert.Null(actual.Directive);continue;}
            Assert.NotNull(actual.Directive);Assert.Equal(directive.GetProperty("name").GetString(),actual.Directive.Name);
            Assert.Equal(Convert.FromBase64String(directive.GetProperty("value").GetString()!),actual.Directive.Value);
            var target=directive.GetProperty("linkTarget");
            if(target.ValueKind==JsonValueKind.Null)Assert.Null(actual.Directive.LinkTarget);
            else Assert.Equal(Convert.FromBase64String(target.GetString()!),actual.Directive.LinkTarget);
        }
    }
    [Fact] public void ReaderDoesNotAliasCallerBytes()
    {
        byte[] input=[0x80];var result=FixtureDirectives.Read(input);input[0]=0x81;
        Assert.Equal((byte)0x80,result.Bytes[0]);Assert.Equal((byte)0x80,result.Lines[0].Bytes[0]);
    }
}
