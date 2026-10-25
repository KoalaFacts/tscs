using System.Text.Json;
namespace Tscs.Conformance;

// JSONL raw-byte protocol for the development harness, not compiler acceptance.
public static class FixtureProtocol
{
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static object Probe(JsonElement request)
    {
        // Validate transport before the parser-panic boundary: malformed requests
        // fail the runner instead of looking like reference parser behavior.
        byte[] source=Convert.FromBase64String(request.GetProperty("input").GetString()!);
        byte[] fileName=Convert.FromBase64String(request.GetProperty("fileName").GetString()!);
        bool implicitFirst=request.GetProperty("implicit").GetBoolean(),returnNil=request.GetProperty("returnNil").GetBoolean();
        int failAt=request.GetProperty("failAt").GetInt32(),calls=0;
        try
        {
            var result=FixtureLoader.Parse(source,fileName,(name,body,options)=>
            {
                calls++;
                return new FixtureCallbackResult<FixtureUnit?>(returnNil?null:new(name,body,new Dictionary<string,byte[]>(options)),
                    calls==failAt?new Exception("callback"):null);
            },implicitFirst);
            var links=result.Links.OrderBy(x=>Convert.ToHexString(x.Alias),StringComparer.Ordinal).ToArray();
            return new{units=result.Units,links,directory=result.Directory,options=result.Options,error=result.Error?.Message,panic=(string?)null,calls};
        }
        catch(InvalidOperationException e){return new{panic=e.Message,calls};}
    }
    public static void Run(TextReader input,TextWriter output)
    {
        while(input.ReadLine() is { } line)
        {
            using var request=JsonDocument.Parse(line);var row=request.RootElement;
            string id=row.GetProperty("id").GetString()??throw new JsonException("Missing identity");
            object expected;
            if(!row.TryGetProperty("profile",out var profile))expected=Probe(row);
            else if(profile.GetString()=="file-map")expected=FixtureFileMap.Probe(row);
            else if(profile.GetString()=="construction")expected=FixtureConstruction.Probe(row);
            else if(profile.GetString()=="source-configurations")expected=FixtureSourceConfigurations.Probe(row);
            else if(profile.GetString()=="config-ast")expected=FixtureConfigAst.Probe(row);
            else throw new JsonException("Unknown fixture profile");
            output.WriteLine(JsonSerializer.Serialize(new{id,expected},JsonOptions));
        }
    }
}
