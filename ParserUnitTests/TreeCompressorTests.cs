using ParserLibrary.Parsers.Common;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests;

public class TreeCompressorTests
{
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
