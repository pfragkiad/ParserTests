using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Compilation;
using ParserLibrary.ExpressionTree;
using Xunit;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;

namespace ParserUnitTests;

public class ParserSession_OptimizationTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_OptimizationTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void Optimize_None_ReturnsUnchanged_NoMetrics()
    {
        var session = GetItemSession();
        session.Expression = "1 + 2";

        // Compile without optimization; returns compiled tree (no metrics API in this path)
        var comp = session.Compile(optimize:false);
        Assert.False(comp.IsOptimized);
    }

    [Fact]
    public void Optimize_StaticTypeMaps_RunsAndProducesTree()
    {
        var session = GetItemSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = 5 };

        // Warm up caches and validate
        var options = new VariableNamesOptions { KnownIdentifierNames = [.. session.Variables.Keys] };
        var report = session.Validate(options);
        Assert.True(report.IsSuccess);

        // Compile with StaticTypeMaps optimization; Compile builds and optimizes tree
        var comp = session.Compile(session.Expression, ExpressionOptimizationMode.StaticTypeMaps, session.Variables);
        Assert.NotNull(comp.Tree);
        Assert.True(comp.Tree!.Root is not null);
    }

    [Fact]
    public void EvaluateWithTreeOptimizer_ReturnsSameAsEvaluate_OnSimpleExpression()
    {
        var session = GetItemSession();
        session.Expression = "1 + 2";

        var report = session.Validate(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);

        var plain = session.Evaluate();
        var optimized = session.Evaluate(optimize:true);
        Assert.Equal(plain, optimized);
    }
}