

using System.Globalization;

namespace ParserUnitTests;

public class UnitTestParser
{



    [Fact]
    public void TestConfigFile()
    {
        var app = App.GetParserApp<Parser>("appsettings2.json");
        var options = app.Services.GetService<IOptions<TokenizerOptions>>().Value;

        Assert.Equal("2.0", options.Version);
    }


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
                { "+",(v1,v2)=>v1+v2} , { "*", (v1, v2) => v1 * v2 },
                { "^",(v1,v2)=>(int)Math.Pow(v1,v2)}  },
            new Dictionary<string, Func<int, int>> {
                { "tan", (v) => 10 * v } ,
                { "sin", (v) => 2 * v }}
            );

        Assert.Equal<int>(860, result);
    }


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
            (s) => double.Parse(s,CultureInfo.InvariantCulture),
            funcs1Arg: new Dictionary<string, Func<double, double>> { { "tan", (v) => 10.0 * v } });

        Assert.Equal(80, result);
    }

    [Fact]
    public void TestCustomParser()
    {
        var app = App.GetParserApp<CustomParser>();
        var parser = app.Services.GetParser();

        string s = "5.0+sin(2,3.0)";

        double result = (double)parser.EvaluateCustom(s);

        Assert.Equal(5.0 + 2 * 3.0, result);
    }

}