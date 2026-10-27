using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class ExpressionGrammarTests
{
    [Theory]
    [InlineData("let b: boolean = true || false && false;")]
    [InlineData("let b: boolean = !false && (true || false);")]
    [InlineData("let b: boolean = 1 + 2 * 3 > 5 && 4 <= 4;")]
    [InlineData("let b: boolean = (1 + 2) * 3 >= 8;")]
    [InlineData("let b: boolean = 4 > 2 && 2 < 4;")]
    [InlineData("let b: boolean = 1 === 1 && 2 !== 3;")]
    [InlineData("let b: boolean = \"alpha\" == \"beta\" || \"alpha\" != \"gamma\";")]
    [InlineData("let b: boolean = true === false;")]
    [InlineData("let b: boolean = !true == false;")]
    [InlineData("let n: number = (1 + 2) * 3;")]
    [InlineData("function select(a: boolean, b: boolean): number { if (a && !b || (1 < 2)) { return 3; } else { return 4; } } let n: number = select(true, false);")]
    [InlineData("function same(a: number, b: number): boolean { return a === b || a <= b; } let result: boolean = same(1, 2);")]
    public void SharedExpressionGrammarChecksExistingLanguageContexts(string source) =>
        Assert.Empty(Compilation.Check("expr.ts",source));

    [Theory]
    [InlineData("let b: boolean = 1 && 2;")]
    [InlineData("let b: boolean = true || 1;")]
    [InlineData("let b: boolean = !1;")]
    [InlineData("let b: boolean = \"a\" < \"b\";")]
    [InlineData("let b: boolean = 1 === \"1\";")]
    [InlineData("let b: boolean = true & false;")]
    [InlineData("let b: boolean = true | false;")]
    [InlineData("let b: boolean = (true || false;")]
    [InlineData("let b: boolean = true ?? false;")]
    public void UnsupportedExpressionRelationsAndGrammarFailClosed(string source) =>
        Assert.Contains(Compilation.Check("expr.ts",source),d=>d.Code=="TSCS1001");

    [Fact]
    public void ExpressionPrecedenceIsPreservedInTheSharedAst()
    {
        var scanner=new Scanner("expr.ts","let ok: boolean = true || false && 1 + 2 < 4 === true;");
        var root=new Parser("expr.ts",scanner.Scan()).Parse();
        var variable=Assert.IsType<Variable>(Assert.Single(root.Statements));
        var disjunction=Assert.IsType<ValueBinary>(variable.Initializer);
        Assert.Equal(TokenKind.BarBar,disjunction.Operator.Kind);
        var conjunction=Assert.IsType<ValueBinary>(disjunction.Right);
        Assert.Equal(TokenKind.AmpAmp,conjunction.Operator.Kind);
        var equality=Assert.IsType<ValueBinary>(conjunction.Right);
        Assert.Equal(TokenKind.StrictEqual,equality.Operator.Kind);
        var comparison=Assert.IsType<ValueBinary>(equality.Left);
        Assert.Equal(TokenKind.LessThan,comparison.Operator.Kind);
        var addition=Assert.IsType<ValueBinary>(comparison.Left);
        Assert.Equal(TokenKind.Plus,addition.Operator.Kind);
    }

    [Fact]
    public void ParenthesesAreRetainedInsteadOfDiscardingSourceStructure()
    {
        var scanner=new Scanner("expr.ts","let n: number = (1 + 2) * 3;");
        var root=new Parser("expr.ts",scanner.Scan()).Parse();
        var variable=Assert.IsType<Variable>(Assert.Single(root.Statements));
        var multiply=Assert.IsType<ValueBinary>(variable.Initializer);
        Assert.Equal(TokenKind.Star,multiply.Operator.Kind);
        var grouped=Assert.IsType<ValueParenthesized>(multiply.Left);
        Assert.Equal(TokenKind.OpenParen,grouped.Open.Kind);
        Assert.Equal(TokenKind.CloseParen,grouped.Close.Kind);
        Assert.Equal(TokenKind.Plus,Assert.IsType<ValueBinary>(grouped.Expression).Operator.Kind);
    }
}
