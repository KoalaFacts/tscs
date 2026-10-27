namespace Tscs;

// The supported function-body grammar contains straight-line statements and
// unconditional lexical blocks. A return in either always completes the body.
// Conditional branches and loops will need explicit path-sensitive flow rules.
internal static class FunctionControlFlow
{
    public static bool DefinitelyReturnsValue(Block body)
    {
        foreach(var statement in body.Statements)
        {
            if(statement is ReturnStatement)return true;
            if(statement is Block nested && DefinitelyReturnsValue(nested))return true;
        }
        return false;
    }
}
