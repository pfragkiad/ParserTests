// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ParserLibrary;

using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;

using System.Diagnostics;
using System.Numerics;
using System.Globalization;
using OneOf;
using ParserTests.Common;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ParserTests.Common.Parsers;



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

    private static IHostBuilder GetHostBuilder(string settingsFile = "appsettings.json")
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                if (settingsFile != "appsettings.json")
                    config.AddJsonFile(settingsFile);  // Configure app configuration if needed
            });
    }

    private static void MainTests()
    {
        //var tokenizer = app.Services.GetTokenizer();

        var tokenizer = ParserApp.GetCommonTokenizer();

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
        var parser = ParserApp.GetDefaultParser();
        var tree = parser!.GetExpressionTree(expr);
        Console.WriteLine("Post order traversal: " + string.Join(" ", tree.Root.PostOrderNodes().Select(n => n.Text)));
        Console.WriteLine("Pre order traversal: " + string.Join(" ", tree.Root.PreOrderNodes().Select(n => n.Text)));
        Console.WriteLine("In order traversal: " + string.Join(" ", tree.Root.InOrderNodes().Select(n => n.Text)));
        //Console.WriteLine(parser.Evaluate(expr));
        tree.Print();

        //TODO: SHOW EXAMPLE WITHOUT SERILOG (show examples using _loggger).
        //TODO: SHOW TREE


        //Console.WriteLine(App.Evaluate("5+2*cos(pi)+3*ln(e)"));
        //ComplexTests();

        //var tokenizerOptions = app.Services.GetRequiredService<IOptions<TokenizerOptions>>().Value;

        var app = ParserApp.GetCommonsApp();
        var tokenizerOptions = app.Services.GetTokenizerOptions();

        Debugger.Break();
    }

    private static void ComplexTests()
    {
        var parser = ParserApp.GetComplexParser();
        string expression = "cos(1+i)";
        var tree = parser.GetExpressionTree(expression);
        tree.Print();

        Complex result = (Complex?)parser.Evaluate("(1+3*i)/(2-3*i)") ?? Complex.Zero;
        Console.WriteLine(result);
        Complex result2 = (Complex?)parser.Evaluate("(1+3*i)/b", new() { { "b", new Complex(2, -3) } }) ?? Complex.Zero;
        Console.WriteLine(result2);
    }

    private static void CheckTypeTests()
    {
        //we need to store the host to keep the scope alive
        var app = ParserApp.GetParserApp<ItemParser>("parsersettings.json");
        IParser parser = app.Services.GetParser();

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


    private static void ItemParserTests()
    {
        // Parser Expression Test
        Console.WriteLine("--- Item Parser Performance Comparison ---");
        var app = ParserApp.GetParserApp<ItemParser>("parsersettings.json");
        IParser parser = app.Services.GetParser();

        var i1 = new Item { Name = "item1", Value = 10 };
        var i2 = new Item { Name = "item2", Value = 5 };

        //string expression = "item1 + 5 + 6 + 7.0 + item2";
        string expression = "(item1*3 +1) + 7.0 + 2.5 + 2*item2";
        var variables = new Dictionary<string, object?>
        {
            { "item1", i1 },
            { "item2", i2 }
        };

        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine($"Variables: item1 = {i1}, item2 = {i2}");
        Console.WriteLine();

        // Pre-generate trees to exclude preparation overhead from performance measurement
        Console.WriteLine("=== Preparing Trees (excluded from performance measurement) ===");
        var originalTree = parser.GetExpressionTree(expression);
        //originalTree.Print(withSlashes: false);

        var variableTypes = new Dictionary<string, Type>
        {
            { "item1", typeof(Item) },
            { "item2", typeof(Item) }
        };
        //var optimizedTree = parser.GetOptimizedTree(expression, variableTypes).Tree;
        var optimizationResult = originalTree.OptimizeForDataTypes(
            parser.TokenizerOptions.TokenPatterns, variableTypes, null);
        var optimizedTree = optimizationResult.Tree;
        optimizedTree.Print();

        Console.WriteLine($"Original Tree: Nodes={originalTree.Count}, Height={originalTree.GetHeight()}");
        Console.WriteLine($"Optimized Tree: Nodes={optimizedTree.Count}, Height={optimizedTree.GetHeight()}");
        Console.WriteLine();

        // Performance comparison - PURE EVALUATION ONLY
        Console.WriteLine("=== Pure Evaluation Performance Comparison ===");
        Console.WriteLine();

        const int iterations = 1;

        //  // Test 1: Standard Evaluate method (without tree optimizer)
        //  Console.WriteLine($"Testing Standard Evaluate method ({iterations} iterations):");

        Console.WriteLine($"Warmup (5 iterations):");
        //// Warm up
        //for (int i = 0; i < 5; i++)
        //{
        //    parser.Evaluate(expression, variables);
        //}

        var evaluateStopwatch = Stopwatch.StartNew();
        object? standardResult = null;

        for (int i = 0; i < iterations; i++)
        {
            standardResult = parser.Evaluate(expression, variables);
        }

        evaluateStopwatch.Stop();

        Console.WriteLine($"  Result: {standardResult} (Type: {standardResult?.GetType().Name})");
        Console.WriteLine($"  Total Time: {evaluateStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Average Time per Evaluation: {(double)evaluateStopwatch.ElapsedTicks / iterations / TimeSpan.TicksPerMillisecond:F6}ms");
        Console.WriteLine($"  Evaluations per second: {iterations * 1000.0 / evaluateStopwatch.ElapsedMilliseconds:F0}");
        Console.WriteLine();

        // Test 2: EvaluateWithTreeOptimizer method
        Console.WriteLine($"Testing EvaluateWithTreeOptimizer method ({iterations} iterations):");

        //// Warm up
        //for (int i = 0; i < 5; i++)
        //{
        //    parser.EvaluateWithTreeOptimizer(expression, variables);
        //}

        var optimizerStopwatch = Stopwatch.StartNew();
        object? optimizedResult = null;

        for (int i = 0; i < iterations; i++)
        {
            optimizedResult = parser.Evaluate(expression, variables,optimizeTree:true);
        }

        optimizerStopwatch.Stop();

        Console.WriteLine($"  Result: {optimizedResult} (Type: {optimizedResult?.GetType().Name})");
        Console.WriteLine($"  Total Time: {optimizerStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Average Time per Evaluation: {(double)optimizerStopwatch.ElapsedTicks / iterations / TimeSpan.TicksPerMillisecond:F6}ms");
        Console.WriteLine($"  Evaluations per second: {iterations * 1000.0 / optimizerStopwatch.ElapsedMilliseconds:F0}");
        Console.WriteLine();

        // Performance analysis
        Console.WriteLine("=== Performance Analysis ===");
        var standardTime = evaluateStopwatch.ElapsedMilliseconds;
        var optimizedTime = optimizerStopwatch.ElapsedMilliseconds;
        var timeDifference = standardTime - optimizedTime;
        var speedupRatio = optimizedTime > 0 ? (double)standardTime / optimizedTime : 0;

        Console.WriteLine($"Standard Evaluate:           {standardTime}ms");
        Console.WriteLine($"EvaluateWithTreeOptimizer:   {optimizedTime}ms");
        Console.WriteLine($"Time Difference:             {timeDifference}ms");
        Console.WriteLine($"Speed Ratio:                 {speedupRatio:F2}x {(speedupRatio > 1 ? "(optimizer is faster)" : "(standard is faster)")}");

        if (speedupRatio > 1)
        {
            Console.WriteLine($"Performance Improvement:     {((speedupRatio - 1) * 100):F1}% faster with optimizer");
        }
        else if (speedupRatio < 1 && speedupRatio > 0)
        {
            Console.WriteLine($"Performance Degradation:     {((1 / speedupRatio - 1) * 100):F1}% slower with optimizer");
        }
        Console.WriteLine();

        // Result validation
        Console.WriteLine("=== Result Validation ===");
        bool resultsMatch = (standardResult?.ToString() == optimizedResult?.ToString()) &&
                           (standardResult?.GetType() == optimizedResult?.GetType());

        Console.WriteLine($"Results Match: {(resultsMatch ? "✓ YES" : "✗ NO")}");
        if (!resultsMatch)
        {
            Console.WriteLine($"Standard Result:  {standardResult} (Type: {standardResult?.GetType().Name})");
            Console.WriteLine($"Optimized Result: {optimizedResult} (Type: {optimizedResult?.GetType().Name})");
        }
        Console.WriteLine($"Expected: Combined Item with value 33 (10 + 5 + 6 + 7 + 5)");
        Console.WriteLine();

        // Show tree structures for reference
        Console.WriteLine("=== Tree Structures ===");
        Console.WriteLine("Original Tree:");
        //originalTree.Print(withSlashes: false);
        //originalTree.Root.PrintSimpleTree();
        //originalTree.Root.PrintAsciiTree();
        //originalTree.Root.PrintHorizontalTree();
        //originalTree.Root.PrintDetailedTree();
        Console.WriteLine("   Vertical:");
        originalTree.Print();
        Console.WriteLine("   Parenthesized:");
        originalTree.Print(PrintType.Parenthesized);
        Console.WriteLine();



        Console.WriteLine("Optimized Tree:");
        //optimizedTree.Root.PrintSimpleTree();
        //optimizedTree.Root.PrintAsciiTree();
        //optimizedTree.Root.PrintHorizontalTree();
        //optimizedTree.Root.PrintDetailedTree();
        Console.WriteLine("   Vertical:");
        optimizedTree.Print(PrintType.Vertical);  // NEW! From NodeBasePrintExtensionsVertical
        Console.WriteLine("   Parenthesized:");
        optimizedTree.Print(PrintType.Parenthesized);
        Console.WriteLine();

        // Final summary
        Console.WriteLine("=== Final Performance Summary ===");
        Console.WriteLine($"Standard Evaluate:           {standardTime}ms ({iterations:N0} iterations)");
        Console.WriteLine($"EvaluateWithTreeOptimizer:   {optimizedTime}ms ({iterations:N0} iterations)");
        Console.WriteLine($"Winner: {(timeDifference > 0 ? "EvaluateWithTreeOptimizer" : "Standard Evaluate")} by {Math.Abs(timeDifference)}ms");
        Console.WriteLine($"Speed Difference: {Math.Abs(speedupRatio - 1) * 100:F1}%");

        Console.WriteLine();
        Console.WriteLine("=== End Pure Evaluation Performance Tests ===");
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


    private static void SimpleFunctionTests()
    {
        var parser = ParserApp.GetParser<SimpleFunctionParser>();

        //string expression = "-8 + add3(5.0,g,3.0)";
        //string expression = "-add(-2,-4)*2+-abs(-2)";
        //string expression = "[TS_3] + max([TS_1],[TS_2]) + 2 + 4";
        string expression = "1 + [TS_2] + 2 + 4 + 6* ([TS_1]+5+2)";

        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine("Original Tree:");
        var tree = parser.GetExpressionTree(expression);
        tree.Print();
        Console.WriteLine("Original Parenthesized:");
        tree.Print(PrintType.Parenthesized);

        // Prepare type maps
        var variableTypes = new Dictionary<string, Type>
        {
            { "TS_1", typeof(Item) },
            { "TS_2", typeof(Item) }
        };

        // If max were present in expression, declare its return type (Item => non-numeric)
        var functionReturnTypes = new Dictionary<string, Type>
        {
            { "max", typeof(Item) } // change to typeof(double) if you want it considered numeric
        };

        // Optimize (extension method now returns TreeOptimizerResult)
        var optimizationResult = tree.OptimizeForDataTypes(
            parser.TokenizerOptions.TokenPatterns,variableTypes, functionReturnTypes);
        var optimizedTree = optimizationResult.Tree;

        //var optimizedResult2 = parser.GetOptimizedExpressionUsingParser(expression, variableTypes);

        Console.WriteLine("\nOptimized Tree:");
        optimizedTree.Print(PrintType.Vertical);
        Console.WriteLine("Optimized Parenthesized:");
        optimizedTree.Print(PrintType.Parenthesized);

        // Expressions (with/without spacing)
        var originalExpression = tree.GetExpressionString(parser.TokenizerOptions);
        var optimizedExpression = optimizedTree.GetExpressionString(parser.TokenizerOptions);

        Console.WriteLine($"\nOriginal expression: {originalExpression}");
        Console.WriteLine($"Optimized expression: {optimizedExpression}");

        Console.WriteLine($"\nNon all-numeric operations: Before={optimizationResult.NonAllNumericBefore}, After={optimizationResult.NonAllNumericAfter}, " +
                          $"Improvement={optimizationResult.Improvement}");

        // Optional: show difference only if improved
        if (optimizationResult.Improvement > 0)
            Console.WriteLine("Optimization reduced mixed (numeric + non-numeric) operations.");
        else if (optimizationResult.Improvement == 0)
            Console.WriteLine("No change in mixed operation count (either already optimal or homogeneous operands).");
        else
            Console.WriteLine("Unexpected increase in mixed operations (review optimizer logic).");


        
    }
    private static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        //MainTests();
        // ComplexTests();


        //return;


        //ItemOperatorTests();

        //ItemParserTests();
        SimpleFunctionTests();

        //CheckTypeTests();



        //get host app with ItemParser

    }

}