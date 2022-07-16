using ParserUnitTests.Parsers;
using System.Diagnostics;

namespace ParserUnitTests;

public class UnitTestParser
{


    [Fact]
    public void TestCustomIntParser()
    {
        var parserApp = App.GetParserApp<IntParser>();
        var parser = parserApp.Services.GetParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = (int)parser.Evaluate(expr, new() { { "a", 8 }, { "asd", 10 } });

        Assert.Equal<int>(860, result);
    }


    [Fact]
    public void TestDoubleIntCustomParser()
    {
        var app = App.GetParserApp<MixedIntDoubleParser>();
        var parser = app.Services.GetParser();

        string s = "5.0+sin(2,3.0)";
        double result = (double)parser.Evaluate(s);

        Assert.Equal(5.0 + 2 * 3.0, result);
    }




    [Theory]
    [InlineData("---5.0", -5.0)]
    [InlineData("(-5.0)", -5.0)]
    [InlineData("-5.0+4.0", -1.0)]
    [InlineData("-add(2,4)", -(2 + 2 * 4))]
    [InlineData("-add(-2,-4)", -(-2 - 4 * 2))]
    [InlineData("-add(-2,-4)*2+-abs(-2)", -(-2 - 4 * 2) * 2 - 2)]
    [InlineData("-pow(2,-2)", -0.25)]
    [InlineData("aDD3(-1,-2,-3)", -1 - 2 * 2 - 3 * 3)]
    [InlineData("-round(10.3513,1)", -10.4)]
    //% in action has higher priority than *, and ! has higher than - [to sum up -> the closest to the operand has the highest priority]
    [InlineData("-!!a%*++2", (-2 * 2 * 5 + 2) * 3 + 2)] //! doubles, % adds 2, * triples (all unary with same priority) 
    public void TestMultipleExpressions(string s, double expected)
    {
        var app = App.GetParserApp<FunctionsOperandsParser>();
        var parser = app.Services.GetParser();
        double result = (double)parser.Evaluate(s, new() { { "a", 5.0 } });
        Assert.Equal(expected, result);

    }

    [Fact]
    public void TestSimpleFunctionParser()
    {
        var parser = App.GetCustomParser<SingleFunctionParser>();
        double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)",
            new() { { "g", 3 } }); // will return 8 + 5 + 2 * 3 + 3 * 3.0

        Assert.Equal(8 + 5 + 2 * 3 + 3 * 3.0, result);
    }

    [Fact]
    public void TestCustomTypeParser()
    {
        var parser = App.GetCustomParser<CustomTypeParser>();
        Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
            new() {
              {"a", new Item { Name="foo"}  },
              {"b", new Item { Name="bar"}  }
            });

        Assert.Equal("foo bar 9", result.ToString());
    }



}