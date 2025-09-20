using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Validation;
using ParserTests.Common.Parsers;
using Xunit;

namespace ParserUnitTests;

public class ParserSession_CompileAndEvaluateTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_CompileAndEvaluateTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetSession() => _fixture.CreateSession();

    [Fact]
    public void Compile_OptimizeFalse_BuildsInfixAndPostfixOnly_SetsState()
    {
        var session = GetSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = 2 };

        var result = session.Compile(optimize: false);

        Assert.NotNull(result);
        Assert.NotEmpty(result.InfixTokens);
        Assert.NotNull(result.PostfixTokens);
        Assert.Null(result.Tree);
        Assert.False(result.IsOptimized);
        Assert.Equal(ParserSessionState.TokenizedPostfix, session.State);
    }

    [Fact]
    public void Compile_OptimizeTrue_BuildsTree_AndRunsOptimizer_SetsState()
    {
        var session = GetSession();
        // Mixed types to allow optimizer to do something useful
        session.Expression = "1 + a + 2";
        session.Variables = new() { ["a"] = new Item { Name = "X", Value = 10 } };

        var result = session.Compile(optimize: true);

        Assert.NotNull(result);
        Assert.NotEmpty(result.InfixTokens);
        Assert.NotNull(result.PostfixTokens);
        Assert.NotNull(result.Tree);
        Assert.True(result.IsOptimized);
        Assert.NotNull(result.OptimizerResult);
        Assert.Equal(ParserSessionState.Optimized, session.State);
    }

    [Fact]
    public void Compile_UsesCachedTree_WhenTreeExists_AndOptimizeFalse()
    {
        var session = GetSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = 3 };

        // Build the tree via Validate (Validate builds tree without optimization)
        var rep = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });
        Assert.True(rep.IsSuccess);
        Assert.NotNull(rep.Tree);

        // Now Compile with optimize=false; should return cached tree
        var comp = session.Compile(optimize: false);
        Assert.NotNull(comp.Tree);
        Assert.Same(rep.Tree, comp.Tree);
    }

    [Fact]
    public void Evaluate_RunValidationTrue_ReturnsValue_AndFinalStateCalculated()
    {
        var session = GetSession();
        session.Expression = "a + 2";
        session.Variables = new() { ["a"] = 3 };

        var value = session.Evaluate(runValidation: true, optimize: false);

        Assert.NotNull(value);
        Assert.Equal(5, Assert.IsType<int>(value));
        Assert.Equal(ParserSessionState.Calculated, session.State);
    }

    [Fact]
    public void Evaluate_RunValidationTrue_FailsValidation_ReturnsNull()
    {
        var session = GetSession();
        // x is not among KnownIdentifierNames (derived from Variables by Evaluate when runValidation==true)
        session.Expression = "a + x";
        session.Variables = new() { ["a"] = 3 };

        var value = session.Evaluate(runValidation: true, optimize: false);

        Assert.Null(value);
        Assert.NotNull(session.ValidationReport);
        Assert.False(session.ValidationReport!.IsSuccess);
    }

    [Fact]
    public void Evaluate_WithOptimizationTrue_StillReturnsCorrectValue()
    {
        var session = GetSession();
        session.Expression = "1 + a + 2";
        session.Variables = new() { ["a"] = 3 };

        var nonOptimized = session.Evaluate(runValidation: true, optimize: false);
        Assert.NotNull(nonOptimized);

        var optimized = session.Evaluate(runValidation: true, optimize: true);
        Assert.NotNull(optimized);

        Assert.Equal(nonOptimized, optimized);
        // Evaluate sets Calculated at the end
        Assert.Equal(ParserSessionState.Calculated, session.State);
    }

    [Fact]
    public void EvaluateType_FromCachedPostfixTree_ReturnsExpectedType_Int()
    {
        var session = GetSession();
        session.Expression = "a + 1";

        // Provide types in variables (EvaluateType consumes Type entries)
        session.Variables = new() { ["a"] = 9 };

        // EvaluateType builds (non-optimized) via Compile(false) internally
        session.Compile(optimize: false);
        var t = session.EvaluateType();
        Assert.Equal(typeof(int), t);
    }

    [Fact]
    public void EvaluateType_ItemPlusInt_ReturnsItemType()
    {
        var session = GetSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = new Item() { Name = "asd", Value = 30} };
        session.Compile(optimize:false);
        var t = session.EvaluateType();
        Assert.Equal(typeof(Item), t);
    }
}