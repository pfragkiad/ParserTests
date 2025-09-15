using ParserLibrary.Parsers;
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

        // Warm up caches without validation
        session.ValidateAndOptimize(session.Expression, runValidation: false);

        var res = session.GetOptimizedTree(ExpressionOptimizationMode.None);
        Assert.NotNull(res.Tree);
        Assert.False(res.HasMetrics);
    }

    [Fact]
    public void Optimize_StaticTypeMaps_RunsAndProducesTree()
    {
        var session = GetItemSession();
        session.Expression = "a + 1";
        session.Variables = new() { ["a"] = 5 };

        // Warm up caches
        session.ValidateAndOptimize(session.Expression, session.Variables, runValidation: false);

        var res = session.GetOptimizedTree(ExpressionOptimizationMode.StaticTypeMaps);
        Assert.NotNull(res.Tree);
        Assert.True(res.Tree.Root is not null);
    }

    [Fact]
    public void EvaluateWithTreeOptimizer_ReturnsSameAsEvaluate_OnSimpleExpression()
    {
        var session = GetItemSession();
        session.Expression = "1 + 2";

        var report = session.Validate(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);

        var plain = session.Evaluate();
        //var optimized = session.EvaluateWithTreeOptimizer();
        var optimized = session.Evaluate(optimizationMode: ExpressionOptimizationMode.ParserInference);
        Assert.Equal(plain, optimized.AsT0);
    }
}