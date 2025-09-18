using ParserLibrary;
using ParserTests.Common;
using ParserTests.Common.Parsers;
using Xunit;

namespace ParserUnitTests;

public class Parser_ItemParser_OptimizationParityTests
{
    [Fact]
    public void OptimizedEvaluation_EqualsPlainEvaluation()
    {
        var parser = ParserApp.GetParser<ItemParser>();

        var i1 = new Item { Name = "item1", Value = 10 };
        var i2 = new Item { Name = "item2", Value = 5 };

        // Mirrors Program.cs expression
        string expression = "(item1*3 +1) + 7.0 + 2.5 + 2*item2";

        var plain = parser.Evaluate(expression, new()
        {
            { "item1", i1 },
            { "item2", i2 }
        });

        var optimized = parser.Evaluate(expression, new()
        {
            { "item1", i1 },
            { "item2", i2 }
        }, optimizeTree: true);

        Assert.NotNull(plain);
        Assert.NotNull(optimized);
        Assert.Equal(plain!.GetType(), optimized!.GetType());
        Assert.Equal(plain!.ToString(), optimized!.ToString());
    }
}