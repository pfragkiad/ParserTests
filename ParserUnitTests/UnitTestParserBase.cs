using Microsoft.Extensions.Hosting;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;

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


  
}
