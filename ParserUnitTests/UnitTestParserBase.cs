using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParserLibrary.Definitions;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests;

public class UnitTestParserBase
{
    [Fact]
    public void TestFunctionWithExpression()
    {
        IParser parser = ParserApp.GetDoubleParser();

        string expression = "a+f10(8+5) + f2(321+asd*2^2)"; //returns 860

        int result = parser.Evaluate<int>(
            expression,
            literalParser: int.Parse,
            variables: new() {
            { "a", 8 },
            { "asd", 10 } },
            binaryOperators: new(){
            { "+",(v1,v2)=>v1+v2} ,
            { "*", (v1, v2) => v1 * v2 },
            { "^",(v1,v2)=>(int)Math.Pow(v1,v2)}  },
            funcs1Arg:
            new() {
            { "f10", (v) => 10 * v } ,
            { "f2", (v) => 2 * v }}
            );

        Assert.Equal<int>(860, result);
    }  
    
    [Fact]
    public void TestFunctionWith2Arguments()
    {
        //IHost app = ParserApp.GetParserApp<ParserBase>();
        //IParser parser = app.GetParser();
        IHost app = ParserApp.GetCommonsApp();
        //IParser parser = app.GetParser("Default");
        IParser parser = app.GetParser("Double");

        string epxression = "a+tan(8+5) * sin(321,asd)"; //returns 43038

        int result = parser.Evaluate<int>(
            epxression,
            literalParser: int.Parse,
            variables: new Dictionary<string, int> {
                { "a", 8 },
                { "asd", 10 } },
            binaryOperators: new Dictionary<string, Func<int, int, int>> {
                { "+",(v1,v2)=>v1+v2} , { "*", (v1, v2) => v1 * v2 },
                { "^",(v1,v2)=>(int)Math.Pow(v1,v2)}  },
            unaryOperators: null,
            funcs1Arg: new Dictionary<string, Func<int, int>> {
                { "tan", (v) => 10 * v } },
            funcs2Arg: new Dictionary<string, Func<int, int, int>> {
                { "sin", (v1,v2) => v1+v2 }}
            );

        Assert.Equal(43038, result);
    }

    [Fact]
    public void TestSimpleIdentifierCase()
    {
        var parserApp = ParserApp.GetParserApp<DoubleParser>();
        IParser parser = parserApp.Services.GetParser();

        string expr = "a";
        int result = parser.Evaluate<int>(
           expr,
           (s) => int.Parse(s),
           new() { { "a", 8 } });

        Assert.Equal(8, result);

    }

    [Fact]
    public void TestCompactFunctionCall()
    {
        var parserApp = ParserApp.GetParserApp<DoubleParser>();
        IParser parser = parserApp.Services.GetParser();

        string expr = "tan(8)";

        double result = parser.Evaluate<double>(
            expr,
            (s) => double.Parse(s, CultureInfo.InvariantCulture),
            funcs1Arg: new() { { "tan", (v) => 10.0 * v } });

        Assert.Equal(80, result);
    }

    [Fact]
    public void LambdaExpressionFactory_UsesConfiguredTokenPatterns()
    {
        var options = new TokenizerOptions
        {
            Version = "1.0",
            TokenPatterns = new TokenPatterns
            {
                CaseSensitive = false,
                Identifier = "[A-Za-z_]\\w*",
                Literal = "\\b(?:\\d+(?:\\.\\d*)?|\\.\\d+)\\b",
                OpenParenthesis = '(',
                CloseParenthesis = ')',
                ArgumentSeparator = ';',
                LambdaArrow = "->",
                Unary = [new() { Name = "-", Priority = 3, Prefix = true }],
                Operators =
                [
                    new() { Name = "+", Priority = 1 },
                    new() { Name = "*", Priority = 2 }
                ]
            }
        };

        using var app = ParserApp.GetParserApp<DoubleParser>(options);
        var factory = app.Services.GetRequiredService<LambdaExpressionFactory>();

        var lambda = factory.TryParse("x;y->x+y");

        Assert.NotNull(lambda);
        Assert.Equal(["x", "y"], lambda!.ParamList);
        Assert.Equal("x+y", lambda.Body);
    }

    [Fact]
    public void DefaultTokenizer_RecognizesLambdaAsNamedLiteral()
    {
        using var app = ParserApp.GetTokenizerApp(TokenizerOptions.Default);
        var tokenizer = app.GetTokenizer();

        var tokens = tokenizer.GetInfixTokens("map(a, (x, y) => x + y)");
        var lambdaToken = Assert.Single(tokens.Where(t => t.CaptureGroup == "lambda"));

        Assert.Equal(TokenType.Literal, lambdaToken.TokenType);
        Assert.Equal("(x, y) => x + y", lambdaToken.Text);
    }


}
