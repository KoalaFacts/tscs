namespace Tscs;

// This bounded flow analysis treats a return as definite only when every
// supported path through blocks and braced conditional arms returns a value.
internal static class FunctionControlFlow
{
    public static bool DefinitelyReturnsValue(Block body)
    {
        foreach(var statement in body.Statements)
            if(AlwaysReturns(statement))return true;
        return false;
    }

    private static bool AlwaysReturns(Statement statement)=>statement switch
    {
        ReturnStatement => true,
        Block nested => DefinitelyReturnsValue(nested),
        ConditionalStatement { Alternate: not null } branch =>
            DefinitelyReturnsValue(branch.Consequent) && AlwaysReturns(branch.Alternate),
        _ => false
    };
}
