// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ParserLibrary;
using ParserLibrary.Parsers;

using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;

using System.Diagnostics;
using System.Numerics;
using ParserTests.Item;
using System.Globalization;
using OneOf;



//TODO: ADD DOCUMENTATION AND PUBLISH IT!


//string expr = "asdf+(2-5*a)* d1-3^2";

//https://www.youtube.com/watch?v=PAceaOSnxQs
//string expr = "K+L-M*N+(O^P)*W/U/V*T+Q";
//string expr = "a*b/c+e/f*g+k-x*y";
//string expr = "2^3^4";
//string expr  = "a+tan(bg)";
//string expr = "a+tan(bg,ab)";
//string expr = "a+tan(a1,a2,a3,a4)"; 
//string expr = "a+sin(1)*tan(8+5,a+2^2*(34-h),98)";
//string expr = "0.1*sin(a1,a2)+90";


internal class Program
{
    private static void MainTests()
    {
        var app = App.GetParserApp<DefaultParser>("parsersettings.json");
        //or to immediately get the parser
        var parser2 = App.GetCustomParser<DefaultParser>("parsersettings.json");

        //var tree = parser.Parse(expr);
        //tree.Root.PrintWithDashes();
        //Console.WriteLine($"Tree nodes: {tree.Count}");
        //Console.WriteLine($"Tree leaf nodes: {tree.CountLeafNodes}");
        //Console.WriteLine($"Tree height: {tree.GetHeight()}");
        //Console.WriteLine("Post order traversal: " + string.Join(" ", tree.Root.PostOrderNodes().Select(n => n.Text)));
        //Console.WriteLine("Pre order traversal: " + string.Join(" ", tree.Root.PreOrderNodes().Select(n => n.Text)));
        //Console.WriteLine("In order traversal: " + string.Join(" ", tree.Root.InOrderNodes().Select(n => n.Text)));

        //expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
        //expr = "a+tan(8+5) * sin(321,asd)"; //returns 43038
        //parser.Parse(expr).Root.PrintWithDashes(0,0);

        string expr = "21--(231)";
        expr = "-2";
        expr = "p------2";
        expr = "a+tan(8+5) + sin(321+afsd*2^2)";
        //expr = "-!!sds%*++2*6";
        //ar tokens = tokenizer.GetInOrderTokens(expr);

        //expr = "-5.0+4.0";
        var parser = App.GetDefaultParser();
        var tokenizer = app.Services.GetTokenizer();
        var tree = parser!.GetExpressionTree(expr);
        Console.WriteLine("Post order traversal: " + string.Join(" ", tree.Root.PostOrderNodes().Select(n => n.Text)));
        Console.WriteLine("Pre order traversal: " + string.Join(" ", tree.Root.PreOrderNodes().Select(n => n.Text)));
        Console.WriteLine("In order traversal: " + string.Join(" ", tree.Root.InOrderNodes().Select(n => n.Text)));
        //Console.WriteLine(parser.Evaluate(expr));
        tree.Print(withSlashes: false);

        //TODO: SHOW EXAMPLE WITHOUT SERILOG (show examples using _loggger).
        //TODO: SHOW TREE


        //Console.WriteLine(App.Evaluate("5+2*cos(pi)+3*ln(e)"));
        //ComplexTests();

        var tokenizerOptions = app.Services.GetRequiredService<IOptions<TokenizerOptions>>().Value;
    }

    private static void ComplexTests()
    {
        var cparser = App.GetCustomParser<ComplexParser>();
        Complex result = (Complex)cparser.Evaluate("(1+3*i)/(2-3*i)");
        Console.WriteLine(result);
        Complex result2 = (Complex)cparser.Evaluate("(1+3*i)/b", new() { { "b", new Complex(2, -3) } });
        Console.WriteLine(result2);
    }

    private static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        MainTests();
        //return;


        //ItemTests
        //CheckTypeTests();



        //get host app with ItemParser

    }

    private static void CheckTypeTests()
    {
        var app = App.GetParserApp<ItemParser>("parsersettings.json");

        IParser parser = app.Services.GetRequiredParser();
        parser.RegisterFunction("myfunc(a,b) = a + b + 10");


        Item item1 = new Item { Name = "foo", Value = 3 };

        var result = parser.Evaluate("a + add(b,4) + 5",
              new() {
                {"a", item1 },
                {"b", new Item { Name="bar"}  }
              });

        //should return  item
        var result2 = parser.Evaluate("a+10",
            new() {
               { "a",  item1 }
            });

        var result3 = parser.Evaluate("a+10.7+90.8",
            new() {
                { "a",  item1 }
            });

        var result4 = parser.Evaluate("myfunc(a,10)",
            new() {
                { "a",  500 }
            });


        object[] results = [result, result2, result3, result4];
        //object[] results  = [result4]; //, result2, result3, result4];
        foreach (var r in results)
            Console.WriteLine($"Result: {r}, Type: {r.GetType()}");

        //now call EvaluateType for all results
        var resultType = parser.EvaluateType("a + add(b,4) + 5",
            new() {
                { "a", item1 },
                { "b", new Item { Name = "bar" } }
            });
        var resultType2 = parser.EvaluateType("a+10",
            new() {
                { "a", item1 }
            });
        var resultType3 = parser.EvaluateType("a+10.7+90.8",
            new() {
                { "a", item1 }
            });

        var resultType4 = parser.EvaluateType("myfunc(a,10)",
            new() {
                { "a", 500 }
            });

        Type[] resultTypes = [resultType, resultType2, resultType3, resultType4];
        //Type[] resultTypes = [resultType4]; //, resultType2, resultType3, resultType4];
        foreach (var rt in resultTypes)
            Console.WriteLine($"Result type: {rt.Name}");


        //Console.WriteLine($"Result: {result}, Type: {result.GetType().Name}");
        //Console.WriteLine($"Result2: {result2}, Type: {result2.GetType().Name}");
        //Console.WriteLine($"Result3: {result3}, Type: {result3.GetType().Name}");
        //Console.WriteLine($"Result4: {result4}, Type: {result4.GetType().Name}");
    }
}