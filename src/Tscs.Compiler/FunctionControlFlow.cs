namespace Tscs;

// The only supported terminating statement is a value return. A successor
// is reachable if any path through the supported blocks can fall through.
internal static class FunctionControlFlow
{
    public static bool DefinitelyReturnsValue(Block body) => !CanCompleteNormally(body);

    public static bool CanCompleteNormally(Block body)
    {
        foreach(var statement in body.Statements)
            if(!CanCompleteNormally(statement))return false;
        return true;
    }

    public static bool CanCompleteNormally(Statement statement) => statement switch
    {
        ReturnStatement => false,
        Block nested => CanCompleteNormally(nested),
        ConditionalStatement { Alternate: { } alternate } branch =>
            CanCompleteNormally(branch.Consequent) || CanCompleteNormally(alternate),
        SwitchStatement switching =>
            !switching.Clauses.Any(clause=>clause.Label==null) ||
            switching.Clauses.Any(clause=>CanCompleteNormally(clause.Body)),
        _ => true
    };
}
