using Microsoft.Extensions.DependencyInjection;
using ParserLibrary;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers;
using ParserTests.Common.Parsers;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace ParserUnitTests;

public class TreeOptimizerTests
{
    // Helper: invoke the protected tree-based Evaluate via reflection
    private static object? EvaluateViaTree(IParser parser, TokenTree tree, Dictionary<string, object?>? variables, bool mergeConstants = true)
    {
        var mi = parser.GetType().GetMethod(
            name: "Evaluate",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(TokenTree), typeof(Dictionary<string, object?>), typeof(bool) },
            modifiers: null);

        if (mi is null)
            throw new InvalidOperationException("Tree-based Evaluate(TokenTree, Dictionary<string, object?>, bool) not found. Implement the protected overload in ParserBase as discussed.");

        return mi.Invoke(parser, new object?[] { tree, variables!, mergeConstants });
    }

    private static void AssertEquivalent(object? postfix, object? tree)
    {
        switch (postfix)
        {
            case double de when tree is double dt:
                Assert.Equal(de, dt, 10);
                break;
            case float fe when tree is float ft:
                Assert.Equal(fe, ft, 5);
                break;
            case Complex ce when tree is Complex ct:
                Assert.Equal(ce, ct);
                break;
            case Item ie when tree is Item it:
                Assert.Equal(ie.Value, it.Value);
                Assert.Equal(ie.ToString(), it.ToString());
                break;
            default:
                Assert.Equal(postfix, tree);
                break;
        }
    }

    [Fact]
    public void TreeVsPostfix_FunctionsOperandsParser()
    {
        var app = ParserApp.GetParserApp<FunctionsOperandsParser>(TokenizerOptions.Default);
        var parser = app.GetParser();

        string expression = "-!!a%*++2";
        var variables = new Dictionary<string, object?> { { "a", 5.0 } };

        var postfixResult = parser.Evaluate(expression, variables);

        var tree = parser.GetExpressionTree(expression);
        var treeResult = EvaluateViaTree(parser, tree, variables);

        AssertEquivalent(postfixResult, treeResult);
    }

    [Fact]
    public void TreeVsPostfix_MixedIntDoubleParser()
    {
        var app = ParserApp.GetParserApp<MixedIntDoubleParser>();
        var parser = app.GetParser();

        string expression = "5.0+sin(2,3.0)";
        var postfixResult = parser.Evaluate(expression);

        var tree = parser.GetExpressionTree(expression);
        var treeResult = EvaluateViaTree(parser, tree, variables: null);

        AssertEquivalent(postfixResult, treeResult);
    }

    [Fact]
    public void TreeVsPostfix_SimpleFunctionParser()
    {
        var app = ParserApp.GetParserApp<SimpleFunctionParser>();
        var parser = app.GetParser();

        string expression = "8 + add3(5.0,g,3.0)";
        var variables = new Dictionary<string, object?> { { "g", 3 } };

        var postfixResult = parser.Evaluate(expression, variables);

        var tree = parser.GetExpressionTree(expression);
        var treeResult = EvaluateViaTree(parser, tree, variables);

        AssertEquivalent(postfixResult, treeResult);
    }

    [Fact]
    public void TreeVsPostfix_ItemParser()
    {
        var parser = ParserApp.GetParser<ItemParser>();

        string expression = "a + add(b,4) + 5";
        var variables = new Dictionary<string, object?>
        {
            {"a", new Item { Name="foo", Value = 3}  },
            {"b", new Item { Name="bar"}  }
        };

        var postfixResult = parser.Evaluate(expression, variables);

        var tree = parser.GetExpressionTree(expression);
        var treeResult = EvaluateViaTree(parser, tree, variables);

        AssertEquivalent(postfixResult, treeResult);
    }

    [Fact]
    public void TreeVsPostfix_ComplexParser()
    {
        var parser = ParserApp.GetComplexParser();

        string expression = "cos(1+i)";

        var postfixResult = parser.Evaluate(expression);

        var tree = parser.GetExpressionTree(expression);
        var treeResult = EvaluateViaTree(parser, tree, variables: null);

        AssertEquivalent(postfixResult, treeResult);
    }

    [Fact]
    public void TreeVsPostfix_OnOptimizedTree_StaticTypeMaps()
    {
        var parser = ParserApp.GetDefaultParser()!;

        string expression = "a + b + 1.0 + c + 2";
        var variables = new Dictionary<string, object?>
        {
            { "a", 4.0 },
            { "b", 5.0 },
            { "c", 6 }
        };
        var variableTypes = new Dictionary<string, Type>
        {
            { "a", typeof(double) },
            { "b", typeof(double) },
            { "c", typeof(int) }
        };

        // Postfix (baseline)
        var postfixResult = parser.Evaluate(expression, variables);

        // Optimized tree should evaluate the same
        var optimizedTree = parser.GetOptimizedExpressionTreeResult(expression, variableTypes).Tree;
        var treeResult = EvaluateViaTree(parser, optimizedTree, variables);

        AssertEquivalent(postfixResult, treeResult);
    }
}