using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Validation;
using Xunit;

namespace ParserUnitTests;

public class ParserSession_Validation_NewApiTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_Validation_NewApiTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetSession() => _fixture.CreateSession();

    [Fact]
    public void Validate_Succeeds_BuildsPostfixAndTree_AndSetsValidatedState()
    {
        var session = GetSession();
        session.Expression = "a + 1";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.True(report.IsSuccess);
        Assert.NotNull(report.PostfixTokens);
        Assert.NotNull(report.Tree);
        Assert.Equal(ParserSessionState.Validated, session.State);
    }

    [Fact]
    public void Validate_Parentheses_Unmatched_Fails_EarlyReturn()
    {
        var session = GetSession();
        session.Expression = "(a + b";

        var report = session.Validate(VariableNamesOptions.Empty, earlyReturnOnErrors: true);

        Assert.False(report.IsSuccess);
        Assert.NotNull(report.ParenthesesResult);
        Assert.False(report.ParenthesesResult!.IsSuccess);
        Assert.Equal(ParserSessionState.Invalid, session.State);
    }

    [Fact]
    public void Validate_OrphanArgumentSeparators_Fails()
    {
        var session = GetSession();
        session.Expression = "a, b";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] });

        Assert.False(report.IsSuccess);
        Assert.NotNull(report.OrphanArgumentSeparatorsResult);
        Assert.False(report.OrphanArgumentSeparatorsResult!.IsSuccess);
        Assert.Equal(ParserSessionState.Invalid, session.State);
    }

    [Fact]
    public void Compile_ThrowsTreeBuildException_OnInvalidPostfixForTree()
    {
        var session = GetSession();
        session.Expression = "a, b";
        session.Variables =  new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };

        _ = session.Compile(optimize: false, forceTreeBuild:true);
        var result = session.Evaluate();
        Assert.Null(result);

        session.Reset();
        _ = session.Compile(optimize: false, forceTreeBuild: false);
        //with postfix calculation a, b should be given as values
        result = session.Evaluate();
        Assert.Null(result);
    }

    [Fact]
    public void Compile_ThrowsTreeBuildException_OnInvalidPostfixForTree2()
    {
        var session = GetSession();
        session.Expression = "(a, b) + 1";
        session.Variables = new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 };

        // Tokenization will succeed; tree build should fail and be mapped to ParserCompileException(TreeBuild)
        _ = session.Compile(optimize: false);
        var ex = Assert.Throws<ArgumentException>(() => session.Evaluate());

    }



    [Fact]
    public void Compile_WithOptimizeFalse_DoesNotRunOptimizer()
    {
        var session = GetSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = 5 };

        var result = session.Compile(optimize: false);

        Assert.NotNull(result);
        Assert.NotEmpty(result.InfixTokens);
        Assert.NotNull(result.PostfixTokens);
        Assert.Null(result.Tree);
        Assert.False(result.IsOptimized);
    }
}