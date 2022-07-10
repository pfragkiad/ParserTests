

using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ParserUnitTests;

public class UnitTestParser
{
    [Fact]
    public void TestFunctionWithExpression()
    {
        var parserApp = App.GetParserApp<Parser>();
        var parser = parserApp.Services.GetParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860

        int result = parser.Evaluate<int>(
            expr,
            (s) => int.Parse(s),
            new Dictionary<string, int> {
                { "a", 8 },
                { "asd", 10 } },
            new Dictionary<string, Func<int, int, int>> {
                { "+",(v1,v2)=>v1+v2} ,
                { "*", (v1, v2) => v1 * v2 },
                { "^",(v1,v2)=>(int)Math.Pow(v1,v2)}  },
            new Dictionary<string, Func<int, int>> {
                { "tan", (v) => 10 * v } ,
                { "sin", (v) => 2 * v }}
            );

        Assert.Equal<int>(860, result);
    }

    #region Custom Parser Int #1

    [Fact]
    public void TestFunctionWithExpressionWithCustomParser()
    {
        var parserApp = App.GetParserApp<TestFunctionWithExpressionParser>();
        var parser = parserApp.Services.GetParser();

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = (int)parser.Evaluate(expr,
            new Dictionary<string, object> {
               { "a", 8 },
              { "asd",10 } }
            );

        Assert.Equal<int>(860, result);
    }

    class TestFunctionWithExpressionParser : Parser
    {
        public TestFunctionWithExpressionParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
        { }

        protected override object EvaluateLiteral(string s)
        {
            return int.Parse(s);
        }

        protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
        {
            int left = (int)nodeValueDictionary[operatorNode.Left as Node<Token>];
            int right = (int)nodeValueDictionary[operatorNode.Right as Node<Token>];
            switch (operatorNode.Text)
            {
                case "+": return left + right;
                case "*": return left * right;
                case "^": return (int)Math.Pow(left, right);
                default: throw new InvalidOperationException($"Unknown operator ({operatorNode.Text})!");
            }
        }

        protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
        {
            int right = (int)nodeValueDictionary[functionNode.Right as Node<Token>];
            switch (functionNode.Text)
            {
                case "tan": return 10 * right;
                case "sin": return 2 * right;
                default: throw new InvalidOperationException($"Unknown function ({functionNode.Text})!");

            }
        }
    }

    #endregion

    #region Custom Parser Int #2
    [Fact]
    public void TestDoubleIntCustomParser()
    {
        var app = App.GetParserApp<CustomIntParser>();
        var parser = app.Services.GetParser();

        string s = "5.0+sin(2,3.0)";
        double result = (double)parser.Evaluate(s);

        Assert.Equal(5.0 + 2 * 3.0, result);
    }
    #endregion

    [Fact]
    public void TestFunctionWith2Arguments()
    {
        var parserApp = App.GetParserApp<Parser>();
        var parser = parserApp.Services.GetParser();

        string expr = "a+tan(8+5) * sin(321,asd)"; //returns 43038

        int result = parser.Evaluate<int>(
            expr,
            (s) => int.Parse(s),
            new Dictionary<string, int> {
                { "a", 8 },
                { "asd", 10 } },
            new Dictionary<string, Func<int, int, int>> {
                { "+",(v1,v2)=>v1+v2} , { "*", (v1, v2) => v1 * v2 },
                { "^",(v1,v2)=>(int)Math.Pow(v1,v2)}  },
            new Dictionary<string, Func<int, int>> {
                { "tan", (v) => 10 * v } },
            new Dictionary<string, Func<int, int, int>> {
                { "sin", (v1,v2) => v1+v2 }}
            );

        Assert.Equal(43038, result);
    }


    [Fact]
    public void TestSimpleIdentifierCase()
    {
        var parserApp = App.GetParserApp<Parser>();
        var parser = parserApp.Services.GetParser();

        string expr = "a";
        int result = parser.Evaluate<int>(
           expr,
           (s) => int.Parse(s),
           new Dictionary<string, int> { { "a", 8 } });

        Assert.Equal(8, result);

    }

    [Fact]
    public void TestCompactFunctionCall()
    {
        var parserApp = App.GetParserApp<Parser>();
        var parser = parserApp.Services.GetParser();

        string expr = "tan(8)";

        double result = parser.Evaluate<double>(
            expr,
            (s) => double.Parse(s, CultureInfo.InvariantCulture),
            funcs1Arg: new Dictionary<string, Func<double, double>> { { "tan", (v) => 10.0 * v } });

        Assert.Equal(80, result);
    }




    [Theory]
    [InlineData("---5.0", -5.0)]
    [InlineData("(-5.0)", -5.0)]
    [InlineData("-5.0+4.0", -1.0)]
    [InlineData("-add(2,4)", -6.0)]
    [InlineData("-add(-2,-4)", 6.0)]
    [InlineData("-add(-2,-4)*2+-abs(-2)", 10.0)]
    [InlineData("-pow(2,-2)", -0.25)]
    [InlineData("aDD3(-1,-2,-3)", -6.0)]
    [InlineData("-round(10.3513,1)",-10.4)]
    public void TestMultipleExpressions(string s, double expected)
    {
        var app = App.GetParserApp<CustomFunctionParser>();
        var parser = app.Services.GetParser();
        double result = (double)parser.Evaluate(s);
        Assert.Equal(expected, result);
    }

    private class CustomFunctionParser : DefaultParser
    {
        public CustomFunctionParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
        {
        }

        protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
        {
            switch (functionNode.Text.ToLower())
            {
                case "add":
                    {
                        //right node is the argument separator
                        double a1 = (double)nodeValueDictionary[functionNode.Right.Left as Node<Token>];
                        double a2 = (double)nodeValueDictionary[functionNode.Right.Right as Node<Token>];
                        return a1 + a2;
                    }
                case "add3":
                    {
                        //right node is the argument separator
                        double a3 = (double)nodeValueDictionary[functionNode.Right.Right as Node<Token>];
                        double a2 = (double)nodeValueDictionary[functionNode.Right.Left.Right as Node<Token>];
                        double a1 = (double)nodeValueDictionary[functionNode.Right.Left.Left as Node<Token>];
                        return a1 + a2 + a3;
                    }
                default:
                    return base.EvaluateFunction(functionNode, nodeValueDictionary);
            }

        }
    }
}