// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ParserLibrary;
using ParserLibrary.Parsers;

using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;

using System.Diagnostics;
using System.Numerics;
using System.Globalization;
using OneOf;
using ParserTests.Common;



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

        Debugger.Break();
    }

    private static void ComplexTests()
    {
        var cparser = App.GetCustomParser<ComplexParser>();
        string expression = "cos(1+i)";
        var tree = cparser.GetExpressionTree(expression);
        tree.Print(withSlashes: false);

        Complex result = (Complex?)cparser.Evaluate("(1+3*i)/(2-3*i)") ?? Complex.Zero;
        Console.WriteLine(result);
        Complex result2 = (Complex?)cparser.Evaluate("(1+3*i)/b", new() { { "b", new Complex(2, -3) } }) ?? Complex.Zero;
        Console.WriteLine(result2);
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


        object?[] results = [result, result2, result3, result4];
        //object[] results  = [result4]; //, result2, result3, result4];
        foreach (var r in results)
            Console.WriteLine($"Result: {r}, Type: {r?.GetType()}");

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


    private static void ItemParserTests(bool withTreeOptimizing)
    {
        // Parser Expression Test
        Console.WriteLine("--- Parser Expression Test ---");
        var app = App.GetParserApp<ItemParser>("parsersettings.json");
        IParser parser = app.Services.GetRequiredParser();

        var i1 = new Item { Name = "I1", Value = 10 };
        var i2 = new Item { Name = "I2", Value = 5 };

        string expression = "I1 + 5 + 6 + 7.0 + I2";
        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine($"Variables: item = {i1}, item2 = {i2}");
        Console.WriteLine();

        // Generate and print the original expression tree with timing
        Console.WriteLine("=== Original Expression Tree ===");
        var treeStopwatch = Stopwatch.StartNew();
        var originalTree = parser.GetExpressionTree(expression);
        treeStopwatch.Stop();
        
        Console.WriteLine($"Tree Info: Nodes={originalTree.Count}, Height={originalTree.GetHeight()}, Leaf Nodes={originalTree.GetLeafNodesCount()}");
        Console.WriteLine($"Tree Generation Time: {treeStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine("\nTree Structure:");
        originalTree.Print(withSlashes: false);
        Console.WriteLine();

        // Generate variable types for optimization
        var variableTypes = new Dictionary<string, Type>
        {
            { "I1", typeof(Item) },
            { "I2", typeof(Item) }
        };

        // Generate and print the optimized expression tree with timing
        Console.WriteLine("=== Optimized Expression Tree ===");
        var optimizedStopwatch = Stopwatch.StartNew();
        var optimizedTree = parser.GetOptimizedExpressionTree(expression, variableTypes);
        optimizedStopwatch.Stop();
        
        Console.WriteLine($"Optimized Tree Info: Nodes={optimizedTree.Count}, Height={optimizedTree.GetHeight()}, Leaf Nodes={optimizedTree.GetLeafNodesCount()}");
        Console.WriteLine($"Optimization Time: {optimizedStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine("\nOptimized Tree Structure:");

        //int requiredHeight = originalTree.GetHeight() + 10; // Add some padding
        //if (Console.BufferHeight < requiredHeight)
        //{
        //    Console.SetBufferSize(Console.BufferWidth, Math.Max(requiredHeight, 50));
        //}
        optimizedTree.Print(withSlashes: false);
        Console.WriteLine();

        // Tree Traversals for comparison
        Console.WriteLine("=== Tree Traversals Comparison ===");
        Console.WriteLine("Original Tree Traversals:");
        Console.WriteLine($"  Post-order: {string.Join(" ", originalTree.Root.PostOrderNodes().Select(n => n.Text))}");
        Console.WriteLine($"  Pre-order:  {string.Join(" ", originalTree.Root.PreOrderNodes().Select(n => n.Text))}");
        Console.WriteLine($"  In-order:   {string.Join(" ", originalTree.Root.InOrderNodes().Select(n => n.Text))}");
        Console.WriteLine();

        Console.WriteLine("Optimized Tree Traversals:");
        Console.WriteLine($"  Post-order: {string.Join(" ", optimizedTree.Root.PostOrderNodes().Select(n => n.Text))}");
        Console.WriteLine($"  Pre-order:  {string.Join(" ", optimizedTree.Root.PreOrderNodes().Select(n => n.Text))}");
        Console.WriteLine($"  In-order:   {string.Join(" ", optimizedTree.Root.InOrderNodes().Select(n => n.Text))}");
        Console.WriteLine();

        // Evaluate the expression with detailed timing
        Console.WriteLine("=== Expression Evaluation ===");
        var evalStopwatch = Stopwatch.StartNew();
        var parserResult = parser.Evaluate(expression, new Dictionary<string, object?>
        {
            { "I1", i1 },
            { "I2", i2 }
        });
        evalStopwatch.Stop();

        Console.WriteLine($"Parser Result: {parserResult}, Type: {parserResult?.GetType().Name}");
        Console.WriteLine($"Expression Evaluation Time: {evalStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total Processing Time: {treeStopwatch.ElapsedMilliseconds + optimizedStopwatch.ElapsedMilliseconds + evalStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Expected calculation: {i1} + 5 + 6 + 7 (rounded) + {i2}");
        Console.WriteLine($"= First 10 + 5 + 6 + 7 + Second 5 = Combined result with total value 33");

        Console.WriteLine();
        Console.WriteLine("=== Performance Summary ===");
        Console.WriteLine($"Tree Generation:     {treeStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Tree Optimization:   {optimizedStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Expression Evaluation: {evalStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total Time:          {treeStopwatch.ElapsedMilliseconds + optimizedStopwatch.ElapsedMilliseconds + evalStopwatch.ElapsedMilliseconds}ms");
        
        Console.WriteLine();
        Console.WriteLine("=== End Item Parser Tests ===");
    }

    private static void ItemOperatorTests()
    {
        Console.WriteLine("=== Item Tests ===");
        Console.WriteLine();

        var item = new Item { Name = "Test", Value = 10 };
        Console.WriteLine($"Original item: {item}");
        Console.WriteLine();

        // Addition with doubles
        var result1 = item + 5.7;    // Value becomes 10 + 6 = 16
        Console.WriteLine($"item + 5.7 = {result1} (5.7 rounded to {(int)Math.Round(5.7, 0)})");

        var result2 = 3.2 + item;    // Value becomes 10 + 3 = 13
        Console.WriteLine($"3.2 + item = {result2} (3.2 rounded to {(int)Math.Round(3.2, 0)})");

        Console.WriteLine();

        // Multiplication with doubles
        var result3 = item * 2.8;    // Value becomes 10 * 3 = 30
        Console.WriteLine($"item * 2.8 = {result3} (2.8 rounded to {(int)Math.Round(2.8, 0)})");

        var result4 = 1.9 * item;    // Value becomes 10 * 2 = 20
        Console.WriteLine($"1.9 * item = {result4} (1.9 rounded to {(int)Math.Round(1.9, 0)})");

        Console.WriteLine();
    }

    private static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        //MainTests();
        // ComplexTests();


        //return;


        //ItemOperatorTests();

        //ItemParserTests();
       
        //CheckTypeTests();



        //get host app with ItemParser

    }

}