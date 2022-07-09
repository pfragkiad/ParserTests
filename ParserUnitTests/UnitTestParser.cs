
namespace ParserUnitTests;

public class UnitTestParser
{
    Parser? _parser; 

    public UnitTestParser()
    {
        var parserApp = App.GetParserApp();
        _parser = parserApp.Services.GetParser();
    }

    [Fact]
    public void TestFunctionWithExpression()
    {

        string expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        int result = _parser.Evaluate<int>(
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
        string expr = "a+tan(8+5) * sin(321,asd)"; //returns 43038

        int result = _parser.Evaluate<int>(
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

       Assert.Equal<int>(43038,result);
    }
}