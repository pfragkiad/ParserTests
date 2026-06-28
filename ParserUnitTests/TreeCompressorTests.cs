using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests;

public class TreeCompressorTests
{
    [Fact]
    public void EvaluateNodeWithDependencies_CompressedRoot_MatchesOriginalEvaluation()
    {
        var parser = (ParserBase)ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        const string expr = "sqrt(sin(a*b+c)*cos(a*b+c)) + sin(a*b+c)*cos(a*b+c) + tan(a*b+c)/(a*b+c)";
        var variables = new Dictionary<string, object?>
        {
            ["a"] = 2.0,
            ["b"] = 3.0,
            ["c"] = 4.0
        };

        var tree = parser.GetExpressionTree(expr);
        var compression = tree.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1);

        Assert.True(compression.IsCompressed);
        Assert.NotNull(compression.CompressedTree);

        var localVariables = new Dictionary<string, object?>(variables);
        var evaluatedCompressed = TokenTree.EvaluateNodeWithDependencies(
            compression,
            compression.CompressedTree!.Root,
            localVariables,
            parser);

        var expected = parser.Evaluate(expr, new Dictionary<string, object?>(variables));
        Assert.Equal(expected, evaluatedCompressed);
    }

    [Fact]
    public void EvaluateNodeWithDependencies_EvaluatesTransitiveDependenciesForSubtree()
    {
        var parser = (ParserBase)ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        const string expr = "sqrt(sin(a*b+c)*cos(a*b+c)) + sin(a*b+c)*cos(a*b+c) + tan(a*b+c)/(a*b+c)";
        var variables = new Dictionary<string, object?>
        {
            ["a"] = 2.0,
            ["b"] = 3.0,
            ["c"] = 4.0
        };

        var tree = parser.GetExpressionTree(expr);
        var compression = tree.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1);

        CompressionEntry? entryWithDependencies = null;
        foreach (var entry in compression.Entries)
        {
            if (entry.Dependencies.Count > 0)
            {
                entryWithDependencies = entry;
                break;
            }
        }

        Assert.NotNull(entryWithDependencies);

        var localVariables = new Dictionary<string, object?>(variables);
        var evaluatedSubtree = TokenTree.EvaluateNodeWithDependencies(
            compression.Entries,
            entryWithDependencies!.SubstitutedSubtree,
            localVariables,
            parser);

        var expected = parser.Evaluate(entryWithDependencies.OriginalExpression, new Dictionary<string, object?>(variables));
        Assert.Equal(expected, evaluatedSubtree);

        foreach (var dependency in entryWithDependencies.Dependencies)
            Assert.True(localVariables.ContainsKey(dependency));
    }

    [Fact]
    public void EvaluateNodeWithDependencies_SubtreeUsingIdentifiers_UsesVariableDependencies()
    {
        var parser = (ParserBase)ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        const string expr = "sin(a*b+r)*cos(a*b+r) + sin(a*b+r)";
        var variables = new Dictionary<string, object?>
        {
            ["a"] = 2.0,
            ["b"] = 3.0,
            ["r"] = 4.0
        };

        var tree = parser.GetExpressionTree(expr);
        var compression = tree.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1);

        CompressionEntry? identifierBasedEntry = null;
        foreach (var entry in compression.Entries)
        {
            bool usesIdentifiers = entry.OriginalExpression.Contains("a", StringComparison.Ordinal)
                || entry.OriginalExpression.Contains("b", StringComparison.Ordinal)
                || entry.OriginalExpression.Contains("r", StringComparison.Ordinal);

            if (usesIdentifiers)
            {
                identifierBasedEntry = entry;
                break;
            }
        }

        Assert.NotNull(identifierBasedEntry);

        var localVariables = new Dictionary<string, object?>(variables);
        var evaluatedSubtree = TokenTree.EvaluateNodeWithDependencies(
            compression,
            identifierBasedEntry!.SubstitutedSubtree,
            localVariables,
            parser);

        var expected = parser.Evaluate(identifierBasedEntry.OriginalExpression, new Dictionary<string, object?>(variables));
        Assert.Equal(expected, evaluatedSubtree);

        Assert.Equal(2.0, localVariables["a"]);
        Assert.Equal(3.0, localVariables["b"]);
        Assert.Equal(4.0, localVariables["r"]);
    }
    [Fact]
    public void Compress_MultiLevel_WithLiterals_ExtractsSameSubstitutionsAsVariables()
    {
        // Mirrors the Program.cs Example 3 (p*q+r) but with numeric literals 1, 2, 3.
        // Anatomy:
        //   LOW-LEVEL  : 1*2  and  1*2+3  appear many times
        //   MID-LEVEL  : cos(1*2+3), sin(1*2+3), sin(1*2+3)*cos(1*2+3)
        //   HIGH-LEVEL : sqrt(sin(1*2+3)*cos(1*2+3))
        //
        // Expected: 4 substitutions (fewer than the 6 with symbolic variables p*q+r).
        // With numeric literals the parser constant-folds 1*2 into 2 before compression,
        // so the low-level atom (1*2) is never seen as a repeated subexpression.
        // The four substitutions that survive are:
        //   _T1 = 1*2+3  (or its folded form)
        //   _T2 = cos(_T1)
        //   _T3 = sin(_T1)
        //   _T4 = _T3*_T2

        var parser = ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        string expr =
            "sqrt(sin(1*2+3)*cos(1*2+3)) * sqrt(sin(1*2+3)*cos(1*2+3))" +
            " + sin(1*2+3)*cos(1*2+3) + sin(1*2+3)*cos(1*2+3)" +
            " + tan(1*2+3) / (1*2+3)";

        var tree = parser.GetExpressionTree(expr);
        var result = tree.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1);

        Assert.Equal(4, result.SubstitutionCount);
    }

    [Fact]
    public void Compress_ForcedFunctionAndOperator_CompressesSingleOccurrences()
    {
        var parser = ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        string expr = "Sin(1)*2";

        var tree = parser.GetExpressionTree(expr);
        var result = tree.Compress(
            patterns,
            tempVarPrefix: "_T",
            minOccurrences: 2,
            forcedFunctions: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sin" },
            forcedOperators: ["*"]);

        Assert.Equal(2, result.SubstitutionCount);
        Assert.Equal(1, result.Entries[0].OccurrenceCount);
        Assert.Equal(1, result.Entries[1].OccurrenceCount);
        Assert.Contains("Sin(", result.Entries[0].SubstitutedExpression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("*", result.Entries[1].SubstitutedExpression, StringComparison.Ordinal);
        Assert.Contains(result.Entries[0].TempVariable, result.Entries[1].SubstitutedExpression, StringComparison.Ordinal);
        Assert.Equal(result.Entries[1].TempVariable, result.CompressedExpression);
    }

    [Fact]
    public void CollectDependencyChainOrdered_ReturnsLeafFirstTopologicalOrder()
    {
        var parser = (ParserBase)ParserApp.GetDefaultParser();
        var patterns = parser.TokenizerOptions.TokenPatterns;

        const string expr = "sqrt(sin(a*b+c)*cos(a*b+c)) + sin(a*b+c)*cos(a*b+c) + tan(a*b+c)/(a*b+c)";

        var tree = parser.GetExpressionTree(expr);
        var compression = tree.Compress(patterns, tempVarPrefix: "_T", minOccurrences: 2, minDepth: 1);

        CompressionEntry? entryWithDependencies = null;
        foreach (CompressionEntry entry in compression.Entries)
        {
            if (entry.Dependencies.Count > 0)
            {
                entryWithDependencies = entry;
                break;
            }
        }

        Assert.NotNull(entryWithDependencies);

        IReadOnlyList<string> ordered = TokenTree.CollectDependencyChainOrdered(
            compression,
            entryWithDependencies!.TempVariable,
            patterns.CaseSensitive);

        Assert.NotEmpty(ordered);

        StringComparer comparer = patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        Dictionary<string, int> indexByTemp = new(comparer);
        for (int i = 0; i < ordered.Count; i++)
            indexByTemp[ordered[i]] = i;

        Dictionary<string, CompressionEntry> entryByTemp = new(compression.Entries.Count, comparer);
        foreach (CompressionEntry entry in compression.Entries)
            entryByTemp[entry.TempVariable] = entry;

        foreach (string dependency in ordered)
        {
            if (!entryByTemp.TryGetValue(dependency, out CompressionEntry? dependencyEntry))
                continue;

            foreach (string nested in dependencyEntry.Dependencies)
            {
                if (!indexByTemp.TryGetValue(nested, out int nestedIndex))
                    continue;

                Assert.True(
                    nestedIndex < indexByTemp[dependency],
                    $"Dependency order invalid: '{dependency}' appears before its prerequisite '{nested}'.");
            }
        }

        if (entryByTemp.TryGetValue(ordered[0], out CompressionEntry? firstEntry))
            Assert.Empty(firstEntry.Dependencies);
    }

    [Fact]
    public void Compress_ForcedFunction_RespectsCaseSensitivity()
    {
        var options = TokenizerOptions.Default;
        options.TokenPatterns.CaseSensitive = true;

        var parser = ParserApp.GetParser<DoubleParser>(options);
        var patterns = parser.TokenizerOptions.TokenPatterns;

        const string expr = "Sin(1)";

        var tree = parser.GetExpressionTree(expr);
        var result = tree.Compress(
            patterns,
            minOccurrences: 2,
            forcedFunctions: ["sin"]);

        Assert.Equal(0, result.SubstitutionCount);
        Assert.Equal(expr, result.CompressedExpression);
    }
}
