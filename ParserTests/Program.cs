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
using ParserLibrary.Parsers.Compilation;



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
        
        //var app = ParserApp.GetParserApp<ItemParser>("parsersettings.json");
        var parser = ParserApp.GetParserSession<ItemParserSession>();
        //IParser parser = app.Services.GetParser();

        var i1 = new Item { Name = "item1", Value = 10 };
        var i2 = new Item { Name = "item2", Value = 5 };

        string expression = "item1 + 5 + 6 + 7.0 + item2*10";
        //string expression = "(item1*3 +1) + 7.0 + 2.5 + 2*item2";
        var variables = new Dictionary<string, object?>
        {
            { "item1", i1 },
            { "item2", i2 }
        };
        parser.Expression = expression;
        parser.Variables = variables;

        Console.WriteLine($"Expression: {expression}");
        Console.WriteLine($"Variables: item1 = {i1}, item2 = {i2}");
        Console.WriteLine();

        // Pre-generate trees to exclude preparation overhead from performance measurement
        Console.WriteLine("=== Preparing Trees (excluded from performance measurement) ===");
        //originalTree.Print(withSlashes: false);

        //var variableTypes = new Dictionary<string, Type>
        //{
        //    { "item1", typeof(Item) },
        //    { "item2", typeof(Item) }
        //};

        //var optimizedTree = parser.GetOptimizedTree(expression, variableTypes).Tree;

        //var optimizationResult = originalTree.OptimizeForDataTypes(
        //    parser.TokenizerOptions.TokenPatterns, variableTypes, null);
        //var optimizedTree = optimizationResult.Tree;
        //optimizedTree.Print();

        // Performance comparison - PURE EVALUATION ONLY

        ParserCompilationResult originalCompileResult = parser.Compile(reset:true, optimize: false, forceTreeBuild:true);
        TokenTree originalTree = originalCompileResult.Tree!;
        object? standardResult = parser.Evaluate();

        ParserCompilationResult compileResult = parser.Compile(reset:true, optimize: true);
        TreeOptimizerResult optimizationResult = compileResult.OptimizerResult!.Value;
        TokenTree optimizedTree = compileResult.Tree!;
        object? optimizedResult = parser.Evaluate();

        Console.WriteLine("=== Pure Evaluation Performance Comparison ===");
        Console.WriteLine();
        Console.WriteLine($"  Result: {standardResult} (Type: {standardResult?.GetType().Name})");
        Console.WriteLine();

        Console.WriteLine($"Testing EvaluateWithTreeOptimizer method:");
        Console.WriteLine($"  Result: {optimizedResult} (Type: {optimizedResult?.GetType().Name})");
        Console.WriteLine();

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

        Console.WriteLine($"Original Tree: Nodes={originalTree.Count}, Height={originalTree.GetHeight()}");
        Console.WriteLine($"Optimized Tree: Nodes={optimizedTree.Count}, Height={optimizedTree.GetHeight()}");
        Console.WriteLine();

        Console.WriteLine("=== Tree Structures ===");
        Console.WriteLine("Original Tree:");
        Console.WriteLine("   Vertical:");
        originalTree.Print();
        Console.WriteLine("   Parenthesized:");
        originalTree.Print(PrintType.Parenthesized);
        Console.WriteLine();

        Console.WriteLine("Optimized Tree:");
        Console.WriteLine("   Vertical:");
        optimizedTree.Print(PrintType.Vertical);  // NEW! From NodeBasePrintExtensionsVertical
        Console.WriteLine("   Parenthesized:");
        optimizedTree.Print(PrintType.Parenthesized);
        Console.WriteLine();

        //show optimization stats
        Console.WriteLine("=== Optimization Statistics ===");
        Console.WriteLine($"\nNon all-numeric operations: Before = {optimizationResult.NonAllNumericBefore}, After = {optimizationResult.NonAllNumericAfter}, " +
                   $"Improvement = {optimizationResult.Improvement}");


        Console.WriteLine($"Optimized Tree Expression: {optimizedTree.GetExpressionString(parser.TokenizerOptions)}");
        // Optional: show difference only if improved
        if (optimizationResult.Improvement > 0)
            Console.WriteLine("Optimization reduced mixed (numeric + non-numeric) operations.");
        else if (optimizationResult.Improvement == 0)
            Console.WriteLine("No change in mixed operation count (either already optimal or homogeneous operands).");
        else
            Console.WriteLine("Unexpected increase in mixed operations (review optimizer logic).");
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
        var parser = ParserApp.GetParserSession<ItemParserSession>();

        //string expression = "-8 + add3(5.0,g,3.0)";
        //string expression = "-add(-2,-4)*2+-abs(-2)";
        //string expression = "[TS_3] + max([TS_1],[TS_2]) + 2 + 4";
        parser.Expression = "1 + [TS_2] + 2 + 4 + 6* ([TS_1]+5+2)";

       // Prepare type maps
        //var variableTypes = new Dictionary<string, Type>
        //{
        //    { "TS_1", typeof(Item) },
        //    { "TS_2", typeof(Item) }
        //};

        //// If max were present in expression, declare its return type (Item => non-numeric)
        //var functionReturnTypes = new Dictionary<string, Type>
        //{
        //    { "max", typeof(Item) } // change to typeof(double) if you want it considered numeric
        //};

        var variables = new Dictionary<string, object?>
        {
            { "TS_1", new Item { Name = "First", Value = 10 } },
            { "TS_2", new Item { Name = "Second", Value = 20 } }
        };

        var tree = parser.Compile(optimize:false, forceTreeBuild:true).Tree!;
        var standardResult = parser.Evaluate(variables);

        TreeOptimizerResult optimizationResult = parser.Compile(reset: true, optimize: true).OptimizerResult!.Value;
        var optimizedTree = optimizationResult.Tree;
        var optimizedResult = parser.Evaluate(variables);

        Console.WriteLine("=== Result Validation ===");
        bool resultsMatch = (standardResult?.ToString() == optimizedResult?.ToString()) &&
                           (standardResult?.GetType() == optimizedResult?.GetType());

        Console.WriteLine($"Results Match: {(resultsMatch ? "✓ YES" : "✗ NO")}");
        if (!resultsMatch)
        {
            Console.WriteLine($"Standard Result:  {standardResult} (Type: {standardResult?.GetType().Name})");
            Console.WriteLine($"Optimized Result: {optimizedResult} (Type: {optimizedResult?.GetType().Name})");
        }
        //var tree = parser.GetExpressionTree(expression);
        // Optimize (extension method now returns TreeOptimizerResult)
        //var optimizationResult = tree.OptimizeForDataTypes(
        //    parser.TokenizerOptions.TokenPatterns,variableTypes, functionReturnTypes);
        //var optimizedTree = optimizationResult.Tree;

        Console.WriteLine($"Expression: {parser.Expression}");
        Console.WriteLine("Original Tree:");
        tree.Print();
        Console.WriteLine("Original Parenthesized:");
        tree.Print(PrintType.Parenthesized);

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
    private static void PrintSubstitutedSubtrees(CompressionResult result, string title)
    {
        Console.WriteLine($"--- Substituted Subtrees ({title}) ---");

        if (result.Entries.Count == 0)
        {
            Console.WriteLine("(none)");
            return;
        }

        foreach (var entry in result.Entries)
        {
            Console.WriteLine($"{entry.TempVariable} = {entry.SubstitutedExpression}");
            entry.SubstitutedSubtree.Print(PrintType.Vertical);
            Console.WriteLine();
        }
    }

    private static void CompressionExample()
    {
        Console.WriteLine("=== Expression Compressor Example ===");
        Console.WriteLine();

        var parser = ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        // ── Example 1: single repeated subexpression ──────────────────────────
        // (a+b*c) appears 4 times; the compressor should extract it into _T1.
        string expr1 = "(a+b*c) * (a+b*c) + sin(a+b*c) / (a+b*c)";
        Console.WriteLine($"Expression 1: {expr1}");
        Console.WriteLine();

        var tree1 = parser.GetExpressionTree(expr1);

        Console.WriteLine($"  Original  ({tree1.Count} nodes, height {tree1.GetHeight()}):");
        tree1.Print(PrintType.Vertical);

        var result1 = tree1.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false);
        Console.WriteLine($"Substitutions found: {result1.SubstitutionCount}");
        Console.WriteLine();

        Console.WriteLine($"  Compressed ({tree1.Count} nodes, height {tree1.GetHeight()}):");
        tree1.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (original, without substitution) ---");
        Console.WriteLine(result1.GetPlanText(withCalculation: false));
        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result1.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result1, "Expression 1");

        // ── Example 2: nested repeated subexpressions ─────────────────────────
        // Both  (x+y)  and  sin(x+y)+cos(x+y)  are repeated → two temp vars expected.
        Console.WriteLine();
        Console.WriteLine("=== Nested Repeated Subexpressions ===");
        Console.WriteLine();

        string expr2 = "(sin(x+y)+cos(x+y)) * (sin(x+y)+cos(x+y)) + (sin(x+y)+cos(x+y)) / (x+y) + tan(x+y)";
        Console.WriteLine($"Expression 2: {expr2}");
        Console.WriteLine();

        var tree2 = parser.GetExpressionTree(expr2);

        Console.WriteLine($"  Original  ({tree2.Count} nodes, height {tree2.GetHeight()}):");
        tree2.Print(PrintType.Vertical);

        CompressionResult result2 = tree2.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false);
        Console.WriteLine($"Substitutions found: {result2.SubstitutionCount}");
        Console.WriteLine();

        Console.WriteLine($"  Compressed ({tree2.Count} nodes, height {tree2.GetHeight()}):");
        tree2.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (original, without substitution) ---");
        Console.WriteLine(result2.GetPlanText(withCalculation: false));
        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result2.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result2, "Expression 2");

        // ── Example 3: multi-level nesting — higher AND lower level repeats ────
        //
        // Anatomy of this expression:
        //
        //   LOW-LEVEL  atoms : p*q  (10×)  and  p*q+r  (10×)
        //   MID-LEVEL  trig  : cos(p*q+r)  (4×)  and  sin(p*q+r)  (4×)
        //   MID-LEVEL  block : sin(p*q+r)*cos(p*q+r)  — 4 occurrences:
        //                        · twice inside sqrt(...)
        //                        · twice standalone
        //   HIGH-LEVEL wrap  : sqrt(sin(p*q+r)*cos(p*q+r))  — 2 occurrences
        //
        //   Compression runs shallowest-first (bottom-up evaluation order):
        //     _T1 = p*q                              [low,  10 occurrences]
        //     _T2 = p*q+r   →  _T1+r                [low,  10 occurrences]
        //     _T3 = cos(p*q+r)  →  cos(_T2)         [mid,   4 occurrences]
        //     _T4 = sin(p*q+r)  →  sin(_T2)         [mid,   4 occurrences]
        //     _T5 = sin*cos  →  _T4*_T3             [mid,   4 occurrences]
        //     _T6 = sqrt(_T5)                        [high,  2 occurrences]
        //
        //   Compressed result: _T6*_T6 + _T5 + _T5 + tan(_T2)/_T2
        //
        //   withCalculation:false  → each line shows only raw source variables (easy to audit)
        //   withCalculation:true   → each line uses previously defined temp vars (ready to evaluate)
        Console.WriteLine();
        Console.WriteLine("=== Multi-Level Nested Repeated Subexpressions ===");
        Console.WriteLine();

        string expr3 =
            "sqrt(sin(p*q+r)*cos(p*q+r)) * sqrt(sin(p*q+r)*cos(p*q+r))" +
            " + sin(p*q+r)*cos(p*q+r) + sin(p*q+r)*cos(p*q+r)" +
            " + tan(p*q+r) / (p*q+r)";

        Console.WriteLine($"Expression 3: {expr3}");
        Console.WriteLine();

        var tree3 = parser.GetExpressionTree(expr3);

        // Print the original tree BEFORE compressing in-place (keepOriginalTree: false mutates this tree).
        Console.WriteLine($"  Original  ({tree3.Count} nodes, height {tree3.GetHeight()}):");
        tree3.Print(PrintType.Vertical);

        // keepOriginalTree: false — no clone, tree3 itself is mutated (faster for large trees).
        // Because tree3 is already printed above, we pass null as originalTree to PrintFull
        // to skip the redundant original-tree section.
        var result3 = tree3.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false);
        Console.WriteLine($"Substitutions found: {result3.SubstitutionCount}");
        Console.WriteLine();

        // tree3 is now the compressed tree (same object as result3.CompressedTree).
        Console.WriteLine($"  Compressed ({tree3.Count} nodes, height {tree3.GetHeight()}):");
        tree3.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (original, without substitution) ---");
        Console.WriteLine(result3.GetPlanText(withCalculation: false));
        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result3.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result3, "Expression 3");

        // ── Example 4: same as Example 3 but with numeric literals 1, 2, 3 ────
        //
        // Because the parser constant-folds 1*2 into 2 before compression,
        // the low-level atom (1*2) is never seen as a repeated subexpression.
        // Only 4 substitutions survive (vs 6 with symbolic variables):
        //   _T1 = 1*2+3  (folded form)
        //   _T2 = cos(_T1)
        //   _T3 = sin(_T1)
        //   _T4 = _T3*_T2
        Console.WriteLine();
        Console.WriteLine("=== Multi-Level Nested Repeated Subexpressions (literals 1, 2, 3) ===");
        Console.WriteLine();

        string expr4 =
            "sqrt(sin(1*2+3)*cos(1*2+3)) * sqrt(sin(1*2+3)*cos(1*2+3))" +
            " + sin(1*2+3)*cos(1*2+3) + sin(1*2+3)*cos(1*2+3)" +
            " + tan(1*2+3) / (1*2+3)";

        Console.WriteLine($"Expression 4: {expr4}");
        Console.WriteLine();

        var tree4 = parser.GetExpressionTree(expr4);

        Console.WriteLine($"  Original  ({tree4.Count} nodes, height {tree4.GetHeight()}):");
        tree4.Print(PrintType.Vertical);

        var result4 = tree4.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false, compressConstantOnlySubtrees: true);
        Console.WriteLine($"Substitutions found: {result4.SubstitutionCount}");
        Console.WriteLine();

        Console.WriteLine($"  Compressed ({tree4.Count} nodes, height {tree4.GetHeight()}):");
        tree4.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (original, without substitution) ---");
        Console.WriteLine(result4.GetPlanText(withCalculation: false));
        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result4.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result4, "Expression 4");

        // ── Example 5: ShouldSkipAssociativeLadder — ladder vs non-ladder ─────
        //
        // ShouldSkipAssociativeLadder suppresses extraction of a repeated subtree when it
        // appears as the dominant operand in an associative chain (+, OR, AND, &):
        //
        //   Criteria (all must hold):
        //     1. Root of the candidate subtree is an associative operator.
        //     2. The flattened operand list has ≥ 3 leaves.
        //     3. At most 2 distinct leaf expressions.
        //     4. The most frequent leaf appears ≥ (total leaves − 1) times.
        //
        // ── 5a. Pure ladder (skipped) ──────────────────────────────────────────
        //   sin(x+y) + sin(x+y) + sin(x+y) + sin(x+y)
        //   Operands: [sin(x+y), sin(x+y), sin(x+y), sin(x+y)]
        //   distinct=1, maxCount=4, total=4  → 1≤2 AND 4≥3  → SKIP → 0 substitutions
        //
        // ── 5b. Near-ladder (also skipped) ────────────────────────────────────
        //   sin(x+y) + sin(x+y) + sin(x+y) + cos(x+y)
        //   Operands: [sin(x+y)×3, cos(x+y)×1]
        //   distinct=2, maxCount=3, total=4  → 2≤2 AND 3≥3  → SKIP → 0 substitutions
        //
        // ── 5c. Non-ladder contrast (compressed) ──────────────────────────────
        //   (sin(x+y) + cos(x+y)) * (sin(x+y) + cos(x+y))
        //   The top-level operator is *, not +, so ShouldSkipAssociativeLadder is false.
        //   x+y  → _T1   (2 occurrences)
        //   sin(x+y)+cos(x+y)  →  sin(_T1)+cos(_T1)  → _T2  (2 occurrences)
        //   Compressed result: _T2*_T2
        Console.WriteLine();
        Console.WriteLine("=== ShouldSkipAssociativeLadder Demo ===");
        Console.WriteLine();

        // 5a — pure ladder: all four operands identical → ladder guard fires, no substitution
        string expr5a = "sin(x+y) + sin(x+y) + sin(x+y) + sin(x+y)";
        Console.WriteLine($"Expression 5a (pure ladder): {expr5a}");
        Console.WriteLine();

        var tree5a = parser.GetExpressionTree(expr5a);

        Console.WriteLine($"  Original  ({tree5a.Count} nodes, height {tree5a.GetHeight()}):");
        tree5a.Print(PrintType.Vertical);

        var result5a = tree5a.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false);
        Console.WriteLine($"Substitutions found: {result5a.SubstitutionCount}");
        Console.WriteLine();

        Console.WriteLine($"  Compressed ({tree5a.Count} nodes, height {tree5a.GetHeight()}):");
        tree5a.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result5a.SubstitutionCount == 0 ? "(no substitutions — ladder guard suppressed extraction)" : result5a.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result5a, "Expression 5a");

        // 5b — near-ladder: 3 of 4 operands identical → ladder guard still fires
        Console.WriteLine();
        string expr5b = "sin(x+y) + sin(x+y) + sin(x+y) + cos(x+y)";
        Console.WriteLine($"Expression 5b (near-ladder): {expr5b}");
        Console.WriteLine();

        var tree5b = parser.GetExpressionTree(expr5b);

        Console.WriteLine($"  Original  ({tree5b.Count} nodes, height {tree5b.GetHeight()}):");
        tree5b.Print(PrintType.Vertical);

        var result5b = tree5b.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false);
        Console.WriteLine($"Substitutions found: {result5b.SubstitutionCount}");
        Console.WriteLine();

        Console.WriteLine($"  Compressed ({tree5b.Count} nodes, height {tree5b.GetHeight()}):");
        tree5b.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result5b.SubstitutionCount == 0 ? "(no substitutions — ladder guard suppressed extraction)" : result5b.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result5b, "Expression 5b");

        // 5c — non-ladder: repeated sum appears under *, not + → ladder guard does NOT fire
        Console.WriteLine();
        string expr5c = "(sin(x+y) + cos(x+y)) * (sin(x+y) + cos(x+y))";
        Console.WriteLine($"Expression 5c (non-ladder, compressed): {expr5c}");
        Console.WriteLine();

        var tree5c = parser.GetExpressionTree(expr5c);

        Console.WriteLine($"  Original  ({tree5c.Count} nodes, height {tree5c.GetHeight()}):");
        tree5c.Print(PrintType.Vertical);

        var result5c = tree5c.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1,
            keepOriginalTree: false);
        Console.WriteLine($"Substitutions found: {result5c.SubstitutionCount}");
        Console.WriteLine();

        Console.WriteLine($"  Compressed ({tree5c.Count} nodes, height {tree5c.GetHeight()}):");
        tree5c.Print(PrintType.Vertical);
        Console.WriteLine();

        Console.WriteLine("--- Plan (original, without substitution) ---");
        Console.WriteLine(result5c.GetPlanText(withCalculation: false));
        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(result5c.GetPlanText(withCalculation: true));
        PrintSubstitutedSubtrees(result5c, "Expression 5c");
    }

    private static void TreeReplacementDemo()
    {
        var parser = ParserApp.GetDefaultParser();

        Console.WriteLine("=== Tree replacement demo ===");

        var tree = parser.GetExpressionTree("a+b");
        Console.WriteLine("Original tree:");
        tree.Print(PrintType.Vertical);
        Console.WriteLine();

        var replacementTree = parser.GetExpressionTree("m*n");
        Console.WriteLine("Replacement subtree:");
        replacementTree.Print(PrintType.Vertical);
        Console.WriteLine();

        tree.ReplaceNode((Node<Token>)tree.Root.Right!, replacementTree);
        Console.WriteLine("Tree after replacing b with the subtree above:");
        tree.Print(PrintType.Vertical);
        Console.WriteLine();

        var tree2 = parser.GetExpressionTree("a+b+a");
        Console.WriteLine("Original tree for replace-all:");
        tree2.Print(PrintType.Vertical);
        Console.WriteLine();

        var replacementNode = new Node<Token>(new Token(TokenType.Identifier, "x", 0));
        int replacedCount = tree2.ReplaceAllNodesAndRebuildOnce(t => t.Text == "a", replacementNode);
        Console.WriteLine($"Replaced {replacedCount} node(s) with x:");
        tree2.Print(PrintType.Vertical);
    }

    private static void Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        //MainTests();
        // ComplexTests();


        //return;


        //ItemOperatorTests();

        //ItemParserTests();
        //SimpleFunctionTests();

        //TreeReplacementDemo();
        CompressionExample();

        //CheckTypeTests();



        //get host app with ItemParser

    }

}