namespace Tscs;

// A bounded structured-flow transfer shared by function-return validation,
// switch fallthrough and guard continuation. Break is consumed by the nearest
// switch/for..of; it must never be mistaken for a function return.
[Flags]
internal enum FlowExit { None=0, Next=1, Return=2, Break=4, Continue=8, Throw=16 }

internal static class FunctionControlFlow
{
    // Every path must terminate abruptly; a throw need not fabricate a return value.
    public static bool DefinitelyReturnsValue(Block body)
    {
        var exits=Analyze(body);
        return (exits&(FlowExit.Next|FlowExit.Break|FlowExit.Continue))==FlowExit.None &&
            (exits&(FlowExit.Return|FlowExit.Throw))!=FlowExit.None;
    }
    public static bool CanCompleteNormally(Block body) => (Analyze(body)&FlowExit.Next)!=0;
    public static bool CanCompleteNormally(Statement statement) => (Analyze(statement)&FlowExit.Next)!=0;

    private static FlowExit Analyze(Block block)
    {
        var paths=FlowExit.Next;
        foreach(var statement in block.Statements)
        {
            if((paths&FlowExit.Next)==0)break;
            paths=(paths&~FlowExit.Next)|Analyze(statement);
        }
        return paths;
    }

    private static FlowExit Analyze(Statement statement) => statement switch
    {
        ReturnStatement => FlowExit.Return,
        ThrowStatement => FlowExit.Throw,
        BreakStatement => FlowExit.Break,
        ContinueStatement => FlowExit.Continue,
        Block body => Analyze(body),
        ConditionalStatement branch => Analyze(branch.Consequent) |
            (branch.Alternate is { } alternate?Analyze(alternate):FlowExit.Next),
        SwitchStatement switching => AnalyzeSwitch(switching),
        // An array loop can iterate zero times. A return inside its body may
        // exit the function, but any break is consumed by the loop itself.
        ForOfStatement loop => FlowExit.Next | (Analyze(loop.Body)&(FlowExit.Return|FlowExit.Throw)),
        // Bounded while conditions may be false initially. Both breaks and
        // continues are consumed; only an actual body return exits the function.
        WhileStatement loop => FlowExit.Next | (Analyze(loop.Body)&(FlowExit.Return|FlowExit.Throw)),
        DoWhileStatement loop => AnalyzeDoWhile(loop),
        ClassicForStatement loop => FlowExit.Next | (Analyze(loop.Body)&(FlowExit.Return|FlowExit.Throw)),
        _ => FlowExit.Next
    };

    private static FlowExit AnalyzeDoWhile(DoWhileStatement loop)
    {
        var body=Analyze(loop.Body);
        // Unlike while, a do body always executes: a guaranteed return does
        // not acquire an invented normal-exit path from its condition.
        return (body&(FlowExit.Return|FlowExit.Throw)) |
            ((body&(FlowExit.Next|FlowExit.Break|FlowExit.Continue))!=0?FlowExit.Next:FlowExit.None);
    }

    private static FlowExit AnalyzeSwitch(SwitchStatement switching)
    {
        // Any case label can be the entry point. A normally completed clause
        // enters its immediate successor; break exits this switch as Next.
        var downstream=FlowExit.Next;
        var exits=FlowExit.None;
        for(int index=switching.Clauses.Count-1;index>=0;index--)
        {
            var local=Analyze(switching.Clauses[index].Body);
            // Continue skips the enclosing loop iteration, not merely the
            // switch clause. Propagate it until an enclosing loop consumes it.
            var current=(local&(FlowExit.Return|FlowExit.Continue|FlowExit.Throw)) |
                ((local&FlowExit.Break)!=0?FlowExit.Next:FlowExit.None) |
                ((local&FlowExit.Next)!=0?downstream:FlowExit.None);
            downstream=current;
            exits|=current;
        }
        // With no default, an unmatched selector can bypass every clause.
        if(!switching.Clauses.Any(clause=>clause.Label==null))
            exits|=FlowExit.Next;
        return exits;
    }
}
