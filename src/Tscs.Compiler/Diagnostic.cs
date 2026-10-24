namespace Tscs;

/// <summary>Locations are UTF-16 offsets, as used by .NET strings and editor clients.</summary>
public sealed record Diagnostic(string Code, string Category, string File, int Start, int Length,
    string Message, Diagnostic[] Related, Diagnostic[] MessageChain)
{
    internal static Diagnostic Error(string file, Token token, string code, string message) =>
        new(code, "error", file, token.Start, token.Length, message, [], []);
}
