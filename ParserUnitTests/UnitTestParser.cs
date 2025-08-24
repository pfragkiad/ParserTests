using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
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
        IParser parser = parserApp.GetParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = (int)parser.Evaluate(expr, new() { { "a", 8 }, { "asd", 10 } })!;

        Assert.Equal<int>(860, result);
    }

    [Fact]
    public void TestCustomIntStatefulParser()
    {
        var app = App.GetStatefulParserApp<IntStatefulParser>();
        var parser = app.GetStatefulParser();
        
        int result =
            (int)parser.Evaluate(
                expression:  "a+tan(8+5) + sin(321+asd*2^2)", 
                variables: new() { { "a", 8 }, { "asd", 10 } })!;

        Assert.Equal<int>(860, result);
    }


    [Fact]
    public void TestDoubleIntCustomParser()
    {
        var app = App.GetParserApp<MixedIntDoubleParser>();
        IParser parser = app.GetParser();

        string s = "5.0+sin(2,3.0)";
        double result = (double)parser.Evaluate(s)!;

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
    //[InlineData("-!!a%*++2", (-2 * 2 * 5 + 2) * 3 + 2)] //! doubles, % adds 2, * triples (all unary with same priority) 
    [InlineData("-!!a%*", (-2 * 2 * 5 + 2) * 3)] //! doubles, % adds 2, * triples (all unary with same priority) 
    public void TestMultipleExpressions(string s, double expected)
    {
        var app = App.GetParserApp<FunctionsOperandsParser>();
        IParser parser = app.GetParser();
        double result = (double)parser.Evaluate(s, new() { { "a", 5.0 } })!;
        Assert.Equal(expected, result);

    }

    [Fact]
    public void TestSimpleFunctionParser()
    {
        var app = App.GetParserApp<SimpleFunctionParser>(); 
        var parser = app.GetParser();
        double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)",
            new() { { "g", 3 } })!; // will return 8 + 5 + 2 * 3 + 3 * 3.0

        Assert.Equal(8 + 5 + 2 * 3 + 3 * 3.0, result);
    }

    [Fact]
    public void TestCustomTypeParser()
    {
        var parser = App.GetParserApp<ItemParser>().GetParser();
        Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
            new() {
                {"a", new Item { Name="foo", Value = 3}  },
                {"b", new Item { Name="bar"}  }
            })!;

        Assert.Equal("foo bar 12", result.ToString());
    }

    [Fact]
    public void TestCustomTypeStatefulParser()
    {
        //Sample adding the library to our own host
        //var app = Host
        //    .CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //     {
        //         services.AddStatefulParserLibrary<CustomTypeStatefulParser>(context);
        //     }).Build();
        //var parser = app.Services.GetStatefulParser();

        //or 
        //var app = App.GetStatefulParserApp<CustomTypeStatefulParser>();
        //var parser = app.Services.GetStatefulParser();

        var app = Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
             {
                 services.AddScoped<ItemStatefulParserFactory>();
             }).Build();
        var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ItemStatefulParserFactory>()!;
        var parser = factory.Create(
            expression: "a + add(b,4) + 5",
            variables: new() {
                {"a", new Item { Name="foo", Value = 3}  },
                {"b", new Item { Name="bar"}  }
             });

        //or
        //var parser = App.GetStatefulParser<ItemStatefulParser>(
        //    expression: "a + add(b,4) + 5",
        //    variables: new() {
        //        {"a", new Item { Name="foo", Value = 3}  },
        //        {"b", new Item { Name="bar"}  }
        //     });
        Item result = (Item)parser.Evaluate()!;

        Assert.Equal("foo bar 12", result.ToString());
    }

    //[Fact]
    //public void TestStatefulParserFactory()
    //{
    //    // Test the new factory approach
    //    var parser = App.CreateStatefulParser<ItemStatefulParser>("a + add(b,4) + 5");

    //    Item result = (Item)parser.Evaluate(
    //        new() {
    //            {"a", new Item { Name="foo", Value = 3}  },
    //            {"b", new Item { Name="bar"}  }
    //        });

    //    Assert.Equal("foo bar 12", result.ToString());
    //}

    [Fact]
    public void TestStatefulParserFactoryWithDifferentExpressions()
    {
        var variables = new Dictionary<string, object?>
        {
            {"a", new Item { Name="test1", Value = 5 }},
            {"b", new Item { Name="test2", Value = 3 }}
        };

        var app = App.GetStatefulParserApp<ItemStatefulParser>();

        var parser1 = app.GetStatefulParser();
        var parser2 = app.GetStatefulParser();

        var result1 = (Item)parser1.Evaluate("a + 10", variables)!;
        var result2 = (Item)parser2.Evaluate("b * 5", variables)!;

        Assert.Equal("test1 15", result1.ToString());
        Assert.Equal("test2 15", result2.ToString()); // 3 * 5 = 15
    }

    [Fact]
    public void TestComplexParser()
    {
        var parser = App.GetComplexParser();

        string expression = "cos(1+i)";
        var result = (Complex)parser.Evaluate(expression)!;
        //var result2 = (Complex)App.EvaluateComplex(expression)!;


        Assert.Equal(Complex.Cos(new Complex(1, 1)), result);

        result = (Complex)parser.Evaluate("cos( (1+i)/(8+9))")!;
        Assert.Equal(Complex.Cos(new Complex(1, 1) / (8 + 9)), result);
    }

}