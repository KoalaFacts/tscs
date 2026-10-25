using System.Text.Json;
using Tscs.Compiler;
using Xunit;

namespace Tscs.Tests;

public class SourceDecoderTests
{
    public static IEnumerable<object[]> ReferenceVectors()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"TestData/source-decoding.json")));
        foreach(var row in data.RootElement.GetProperty("vectors").EnumerateArray())
            yield return new object[]{row.GetProperty("id").GetString()!,row.GetProperty("input").GetString()!,row.GetProperty("expected").GetString()!};
    }
    [Theory,MemberData(nameof(ReferenceVectors))]
    public void DecodedBytesMatchPinnedGo(string id,string input,string expected)
    {
        Assert.NotEmpty(id);
        Assert.Equal(Convert.FromBase64String(expected),SourceDecoder.Decode(Convert.FromBase64String(input)));
    }
    [Fact] public void ReturnedBytesDoNotAliasInput()
    {
        byte[] input=[0x80];byte[] output=SourceDecoder.Decode(input);output[0]=0x81;
        Assert.Equal((byte)0x80,input[0]);
    }
}
