using ParserLibrary;
using ParserLibrary.ExpressionTree;
using ParserTests.Common;
using ParserTests.Common.Parsers;
using Xunit;

namespace ParserUnitTests;

public class Parser_SimpleFunctionParser_OptimizationTests2
{
    [Fact]
    public void OptimizeForDataTypes_ChangesExpressionString_ButRetainsSemanticResult_ViaTreeEvaluation()
    {
        var parser = ParserApp.GetParser<SimpleFunctionParser>();

        // Expression chosen to allow numeric regrouping / simplification
        // Expect optimizer to fold (1 + 2 + 4) and (5 + 2) parts, potentially reordering
        string expression = "1 + [TS_2] + 2 + 4 + 6* ([TS_1]+5+2)";
        var originalTree = parser.GetExpressionTree(expression);

        var variableTypes = new Dictionary<string, Type>
        {
            { "TS_1", typeof(Item) },
            { "[TS_1]", typeof(Item) },
            { "TS_2", typeof(Item) },
            { "[TS_2]", typeof(Item) }
        };

        var functionReturnTypes = new Dictionary<string, Type>
        {
            { "max", typeof(Item) }
        };

        // Run optimization
        var optimizationResult = originalTree.OptimizeForDataTypes(
            parser.TokenizerOptions.TokenPatterns,
            variableTypes,
            functionReturnTypes);

        var optimizedTree = optimizationResult.Tree;

        var originalExpr = originalTree.GetExpressionString(parser.TokenizerOptions);
        var optimizedExpr = optimizedTree.GetExpressionString(parser.TokenizerOptions);

        // We now EXPECT a different textual form (normalization / regrouping / folding)
        Assert.NotEqual(originalExpr, optimizedExpr);

        // Evaluate BOTH via tree-based path (force tree evaluation by using optimizeTree:true call)
        // Provide dummy variable values (Items) since identifiers appear
        var vars = new Dictionary<string, object?>
        {
            { "TS_1", new Item { Name = "A", Value = 10 } },
            { "TS_2", new Item { Name = "B", Value = 5 } },
            { "[TS_1]", new Item { Name = "A", Value = 10 } }, // if tokenizer keeps brackets
            { "[TS_2]", new Item { Name = "B", Value = 5 } }
        };

        // Evaluate original (will internally rebuild & optimize again, but still tree-based)
        var originalValue = parser.Evaluate(originalExpr, vars, optimizeTree: true);
        var optimizedValue = parser.Evaluate(optimizedExpr, vars, optimizeTree: true);

        Assert.NotNull(originalValue);
        Assert.NotNull(optimizedValue);
        Assert.Equal(originalValue!.GetType(), optimizedValue!.GetType());
        Assert.Equal(originalValue.ToString(), optimizedValue.ToString());

        // Metrics sanity
        Assert.True(optimizationResult.Improvement >= 0);
        Assert.True(optimizationResult.NonAllNumericAfter <= optimizationResult.NonAllNumericBefore);
    }
}