using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers;
using ParserTests.Common.Parsers;
using System.Diagnostics;
using System.Numerics;

namespace ParserUnitTests;

public class UnitTestParser
{


    [Fact]
    public void TestCustomIntParser()
    {
        var parserApp = ParserApp.GetParserApp<IntParser>();
        IParser parser = parserApp.GetParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = (int)parser.Evaluate(expr, new() { { "a", 8 }, { "asd", 10 } })!;

        Assert.Equal<int>(860, result);
    }

    [Fact]
    public void TestCustomIntParserSession()
    {
        var app = ParserApp.GetParserSessionApp<IntParserSession>();
        var parser = app.GetParserSession();

        int result =
            (int)parser.Evaluate(
                expression: "a+tan(8+5) + sin(321+asd*2^2)",
                variables: new() { { "a", 8 }, { "asd", 10 } })!;

        Assert.Equal<int>(860, result);
    }


    [Fact]
    public void TestDoubleIntCustomParser()
    {
        var app = ParserApp.GetParserApp<MixedIntDoubleParser>();
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
    [InlineData("-!!a%*++2", (-2 * 2 * 5 + 2) * 3 + 2)] //! doubles, % adds 2, * triples (all unary with same priority) 
    [InlineData("-!!a%*", (-2 * 2 * 5 + 2) * 3)] //! doubles, % adds 2, * triples (all unary with same priority) 
    public void TestMultipleExpressions(string s, double expected)
    {
        var app = ParserApp.GetParserApp<FunctionsOperandsParser>(TokenizerOptions.Default);


        IParser parser = app.GetParser();
        double result = (double)parser.Evaluate(s, new() { { "a", 5.0 } })!;
        Assert.Equal(expected, result);

    }

    [Fact]
    public void TestPostfixExpression()
    {
        var app = ParserApp.GetParserApp<FunctionsOperandsParser>(TokenizerOptions.Default);
        IParser parser = app.GetParser();

        string expression = "-!!a%*++2";
        var tree = parser.GetExpressionTree(expression);
        tree.Print();


        double result = (double)parser.Evaluate(
            expression,
            new() { { "a", 5.0 } })!; // will return (-2 * 2 * 5 + 2) * 3 + 2
        Assert.Equal((-2 * 2 * 5 + 2) * 3 + 2, result);
    }   

    [Fact]
    public void TestSimpleFunctionParser()
    {
        var app = ParserApp.GetParserApp<SimpleFunctionParser>();
        var parser = app.GetParser();

        string expression = "8 + add3(5.0,g,3.0)";

        var tree = parser.GetExpressionTree(expression);
        var treeVerticalString = tree.Root.ToVerticalTreeString();
        Debugger.Break();

        double result = (double)parser.Evaluate(expression ,
            new() { { "g", 3 } })!; // will return 8 + 5 + 2 * 3 + 3 * 3.0

        Assert.Equal(8 + 5 + 2 * 3 + 3 * 3.0, result);
    }

    [Fact]
    public void TestSimpleFunctionParser_Host()
    {
        var app = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddParser<SimpleFunctionParser>(context);
            }).Build();
        var parser = app.GetParser();
        double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)",
            new() { { "g", 3 } })!; // will return 8 + 5 + 2 * 3 + 3 * 3.0

        Assert.Equal(8 + 5 + 2 * 3 + 3 * 3.0, result);
    }

    [Fact]
    public void TestSimpleFunctionParser_Host_Keyed()
    {
        var app = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddParser<SimpleFunctionParser>("simple", TokenizerOptions.Default);
                services.AddParser<ComplexParser>("complex", TokenizerOptions.Default);
            }).Build();
        var parser = app.GetParser("simple");
        double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)",
            new() { { "g", 3 } })!; // will return 8 + 5 + 2 * 3 + 3 * 3.0

        Assert.Equal(8 + 5 + 2 * 3 + 3 * 3.0, result);
    }

    [Fact]
    public void TestCustomTypeParser()
    {
        //var parser = ParserApp.GetParserApp<ItemParser>().GetParser();
        
        var parser = ParserApp.GetParser<ItemParser>();

        //var host  = Host.CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //     {
        //         services.AddParser<ItemParser>(context);
        //     }).Build();
        //var parser2 = host.GetParser();

        //host with keyed parser named "item"
        //var host = Host.CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //    {
        //        services.AddParser<ItemParser>("item", TokenizerOptions.Default);
        //    }).Build();
        //var parser = host.GetParser("item");


        Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
            new() {
                {"a", new Item { Name="foo", Value = 3}  },
                {"b", new Item { Name="bar"}  }
            })!;

        Assert.Equal("foo bar 12", result.ToString());
    }

    [Fact]
    public void TestCustomTypeParserSession()
    {
        //fast build a host and get the parser immediately
        //var parser = ParserApp.GetParserSessionApp<ItemParserSession>().GetParserSession();
        var parser = ParserApp.GetParserSession<ItemParserSession>();

        //var host = Host.CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //     {
        //         services.AddParserSession<ItemParserSession>(context);
        //     }).Build();
        //var parser = host.GetParserSession();


        string expression = "a + add(b,4) + 5";
        Dictionary<string, object?> variables = new()
        {
            {"a", new Item { Name="foo", Value = 3}  },
            {"b", new Item { Name="bar"}  }
        };
        //Item result = (Item)parser.Evaluate(expression, variables)!;

        parser.Expression = expression;

        //set the variables later (perhaps after a validation)
        Item result = (Item)parser.Evaluate(variables).AsT0!;



        Assert.Equal("foo bar 12", result.ToString());
    }

    //[Fact]
    //public void TestStatefulParserFactory()
    //{
    //    // Test the new factory approach
    //    var parser = App.CreateStatefulParser<ItemParserSession>("a + add(b,4) + 5");

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

        var app = ParserApp.GetParserSessionApp<ItemParserSession>();

        var parser1 = app.GetParserSession();
        var parser2 = app.GetParserSession();

        var result1 = (Item)parser1.Evaluate("a + 10", variables)!;
        var result2 = (Item)parser2.Evaluate("b * 5", variables)!;

        Assert.Equal("test1 15", result1.ToString());
        Assert.Equal("test2 15", result2.ToString()); // 3 * 5 = 15
    }

    [Fact]
    public void TestComplexParser()
    {
        var parser = ParserApp.GetComplexParser();

        string expression = "cos(1+i)";
        var result = (Complex)parser.Evaluate(expression)!;
        //var result2 = (Complex)App.EvaluateComplex(expression)!;


        Assert.Equal(Complex.Cos(new Complex(1, 1)), result);

        result = (Complex)parser.Evaluate("cos( (1+i)/(8+9))")!;
        Assert.Equal(Complex.Cos(new Complex(1, 1) / (8 + 9)), result);
    }

}