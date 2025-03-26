using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers;
using ParserUnitTests.Parsers;
using System.Diagnostics;
using System.Numerics;

namespace ParserUnitTests;

public class UnitTestParser
{


    [Fact]
    public void TestCustomIntParser()
    {
        var parserApp = App.GetParserApp<IntParser>();
        IParser parser = parserApp.Services.GetRequiredParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = (int)parser.Evaluate(expr, new() { { "a", 8 }, { "asd", 10 } });

        Assert.Equal<int>(860, result);
    }

    [Fact]
    public void TestCustomIntTransientParser()
    {
        var parserApp = App.GetTransientParserApp<IntTransientParser>();
        ITransientParser parser = parserApp.Services.GetRequiredTransientParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = (int)parser.Evaluate(expr, new() { { "a", 8 }, { "asd", 10 } });

        Assert.Equal<int>(860, result);
    }


    [Fact]
    public void TestDoubleIntCustomParser()
    {
        var app = App.GetParserApp<MixedIntDoubleParser>();
        IParser parser = app.Services.GetRequiredParser();

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
        IParser parser = app.Services.GetRequiredParser();
        double result = (double)parser.Evaluate(s, new() { { "a", 5.0 } });
        Assert.Equal(expected, result);

    }

    [Fact]
    public void TestSimpleFunctionParser()
    {
        var parser = App.GetCustomParser<SimpleFunctionParser>();
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
                {"a", new Item { Name="foo", Value = 3}  },
                {"b", new Item { Name="bar"}  }
            });

        Assert.Equal("foo bar 12", result.ToString());
    }

    [Fact]
    public void TestCustomTypeTransientParser()
    {
        //Sample adding the library to our own host
        //var app = Host
        //    .CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //     {
        //         services.AddTransientParserLibrary<CustomTypeTransientParser>(context);
        //     }).Build();
        //var parser = app.Services.GetTransientParser();

        //or 
        //var app = App.GetTransientParserApp<CustomTypeTransientParser>();
        //var parser = app.Services.GetTransientParser();

        //or
        var parser = App.GetCustomTransientParser<CustomTypeTransientParser>();

        Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
            new() {
                {"a", new Item { Name="foo", Value = 3}  },
                {"b", new Item { Name="bar"}  }
            });

        Assert.Equal("foo bar 12", result.ToString());
    }

    [Fact]
    public void TestComplexParser()
    {
        //Complex c1 = new(1, 1);

        var cparser = App.GetCustomParser<ComplexParser>();
        var result = (Complex)cparser.Evaluate("cos(1+i)");

        Assert.Equal(Complex.Cos(new Complex(1, 1)), result);

        result = (Complex)cparser.Evaluate("cos( (1+i)/(8+9))");
        Assert.Equal(Complex.Cos(new Complex(1, 1) / (8 + 9)), result);
    }

}