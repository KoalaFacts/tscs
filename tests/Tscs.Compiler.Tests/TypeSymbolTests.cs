using Tscs;
using Xunit;

namespace Tscs.Tests;

public sealed class TypeSymbolTests
{
    [Fact]
    public void PrimitiveTypeIdentitiesAreCanonicalAndAssignableOnlyWithinKnownRules()
    {
        Assert.Same(CompilerTypes.Number,CompilerTypes.FromAnnotation("number"));
        Assert.Same(CompilerTypes.String,CompilerTypes.FromLiteral(new Token(TokenKind.String,"\"x\"",0,3)));
        Assert.Same(CompilerTypes.Boolean,CompilerTypes.FromLiteral(new Token(TokenKind.False,"false",0,5)));
        Assert.Null(CompilerTypes.FromLiteral(new Token(TokenKind.Identifier,"missing",0,7)));
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Number,CompilerTypes.Any));
        Assert.True(CompilerTypes.IsAssignable(CompilerTypes.Any,CompilerTypes.String));
        Assert.False(CompilerTypes.IsAssignable(CompilerTypes.Number,CompilerTypes.String));
        Assert.True(CompilerTypes.IsNumber(CompilerTypes.Number));
        Assert.False(CompilerTypes.IsNumber(CompilerTypes.Any));
        Assert.Throws<ArgumentOutOfRangeException>(()=>CompilerTypes.FromAnnotation("unknown"));
    }
    [Fact]
    public void BoundedFunctionAssignmentUsesContravariantInputsAndCovariantReturns()
    {
        var narrow=new FunctionTypeSymbol([CompilerTypes.Number],CompilerTypes.Number);
        var broadInput=new FunctionTypeSymbol([CompilerTypes.Any],CompilerTypes.Number);
        var stringReturn=new FunctionTypeSymbol([CompilerTypes.Number],CompilerTypes.String);
        var twoArgs=new FunctionTypeSymbol([CompilerTypes.Number,CompilerTypes.Number],CompilerTypes.Number);
        Assert.True(CompilerTypes.IsAssignable(narrow,new FunctionTypeSymbol([CompilerTypes.Number],CompilerTypes.Number)));
        Assert.True(CompilerTypes.IsAssignable(broadInput,narrow));
        Assert.False(CompilerTypes.IsAssignable(narrow,stringReturn));
        Assert.False(CompilerTypes.IsAssignable(narrow,twoArgs));
        Assert.True(CompilerTypes.IsEquivalent(narrow,new FunctionTypeSymbol([CompilerTypes.Number],CompilerTypes.Number)));
    }


}
