using Microsoft.Extensions.Hosting;
using ParserLibrary;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using ParserTests.Common.Parsers;
using ParserLibrary.Parsers.Validation;
using Xunit;
using System.Text.RegularExpressions;

namespace ParserUnitTests;

public sealed class ItemSessionFixture : IDisposable
{
    public IHost Host { get; }

    public ItemSessionFixture()
    {
        Host = ParserApp.GetParserSessionApp<ItemParserSession>(TokenizerOptions.Default);
    }

    public ParserSessionBase CreateSession() => (ParserSessionBase)Host.GetParserSession();

    public void Dispose() => Host.Dispose();
}

public class ParserSessionValidationTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;

    public ParserSessionValidationTests(ItemSessionFixture fixture) => _fixture = fixture;

    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void ValidateParentheses_Fails_OnUnmatched()
    {
        var session = GetItemSession();
        session.Expression = "(a + (b * 2)";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = [] }, earlyReturnOnErrors: true);

        Assert.False(report.ParenthesesResult!.IsSuccess);
        Assert.NotEmpty(report.ParenthesesResult.GetValidationFailures());
    }

    [Fact]
    public void CheckVariableNames_PartialMatch_UnmatchedDetected()
    {
        var session = GetItemSession();
        session.Expression = "a + b + c";

        var opts = new VariableNamesOptions { KnownIdentifierNames = [ "a", "b" ] };
        var report = session.Validate(opts);

        Assert.False(report.VariableNamesResult!.IsSuccess);
        Assert.Contains("c", report.VariableNamesResult.UnmatchedNames);
    }

    [Fact]
    public void CheckFunctionNames_UnmatchedDetected()
    {
        var session = GetItemSession();
        session.Expression = "foo(1) + add(b,4)";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["b"] });

        Assert.False(report.FunctionNamesResult!.IsSuccess);
        Assert.Contains("foo", report.FunctionNamesResult.UnmatchedNames);
        Assert.Contains("add", report.FunctionNamesResult.MatchedNames);
    }
        
    [Fact]
    public void CheckEmptyFunctionArguments_Detected_ForTrailingOrLeadingEmpty()
    {
        var session = GetItemSession();

        session.Expression = "add(,4)";
        var report1 = session.Validate(new VariableNamesOptions { KnownIdentifierNames = [] });
        Assert.False(report1.EmptyFunctionArgumentsResult!.IsSuccess);

        session.Expression = "add(4,)";
        var report2 = session.Validate(new VariableNamesOptions { KnownIdentifierNames = [] });
        Assert.False(report2.EmptyFunctionArgumentsResult!.IsSuccess);
    }

    [Fact]
    public void CheckFunctionArgumentsCount_Invalid_ForTooFewArgs()
    {
        var session = GetItemSession();
        session.Expression = "add(1)";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = [] });

        Assert.False(report.FunctionArgumentsCountResult!.IsSuccess);
        Assert.NotEmpty(report.FunctionArgumentsCountResult.InvalidFunctions);
    }

    [Fact]
    public void CheckOperatorOperands_Succeeds_OnValidExpression()
    {
        var session = GetItemSession();
        session.Expression = "a + 1";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.True(report.OperatorOperandsResult!.IsSuccess);
        Assert.Empty(report.OperatorOperandsResult.InvalidOperators);
    }

    [Fact]
    public void CheckOrphanArgumentSeparators_Detected()
    {
        var session = GetItemSession();
        session.Expression = "a, b";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] });

        Assert.False(report.OrphanArgumentSeparatorsResult!.IsSuccess);
        Assert.NotEmpty(report.OrphanArgumentSeparatorsResult.GetValidationFailures());
    }

    [Fact]
    public void Validate_Aggregate_RespectsProvidedKnownNames_AndEarlyReturn()
    {
        var session = GetItemSession();

        session.Expression = "(a + b";
        var reportParen = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] }, earlyReturnOnErrors: true);
        Assert.False(reportParen.ParenthesesResult!.IsSuccess);

        session.Expression = "a + b";
        session.Variables = new() { { "a", 1 }, { "b", 2 } };

        var reportVars = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });
        Assert.False(reportVars.VariableNamesResult!.IsSuccess);
        Assert.Contains("b", reportVars.VariableNamesResult.UnmatchedNames);
    }

    // Exhaustive VariableNamesOptions cases

    [Fact]
    public void VariableNames_StrictMatching_NoIgnores()
    {
        var session = GetItemSession();
        session.Expression = "a + b + c";

        var report = session.Validate(new VariableNamesOptions
        {
            KnownIdentifierNames = [ "a", "c" ],
            IgnoreCaptureGroups = null,
            IgnoreIdentifierPattern = null,
            IgnorePrefixes = null,
            IgnorePostfixes = null
        });

        var res = report.VariableNamesResult!;
        Assert.Contains("a", res.MatchedNames);
        Assert.Contains("c", res.MatchedNames);
        Assert.Contains("b", res.UnmatchedNames);
        Assert.Empty(res.IgnoredNames);
    }

    [Fact]
    public void VariableNames_IgnorePrefixes_And_Postfixes()
    {
        var session = GetItemSession();
        session.Expression = "aX + pre_b + c_suf + keep";

        var report = session.Validate(new VariableNamesOptions
        {
            KnownIdentifierNames = [ "keep" ],
            IgnorePrefixes = new[] { "pre_" },
            IgnorePostfixes = new[] { "_suf", "X" }
        });

        var res = report.VariableNamesResult!;
        Assert.Contains("keep", res.MatchedNames);
        Assert.Contains("pre_b", res.IgnoredNames);
        Assert.Contains("c_suf", res.IgnoredNames);
        Assert.Contains("aX", res.IgnoredNames);
        Assert.Empty(res.UnmatchedNames);
    }

    [Fact]
    public void VariableNames_IgnoreRegexPattern()
    {
        var session = GetItemSession();
        session.Expression = "tmp_a + TMP + keep + TmpVar";

        var pattern = new Regex("^(tmp_.*|[A-Z]+)$");
        var report = session.Validate(new VariableNamesOptions
        {
            KnownIdentifierNames = [ "keep" ],
            IgnoreIdentifierPattern = pattern
        });

        var res = report.VariableNamesResult!;
        Assert.Contains("keep", res.MatchedNames);
        Assert.Contains("tmp_a", res.IgnoredNames);
        Assert.Contains("TMP", res.IgnoredNames);
        Assert.Contains("TmpVar", res.UnmatchedNames);
    }

    [Fact]
    public void VariableNames_IgnoreCaptureGroups_CustomTokenizer()
    {
        var custom = new TokenizerOptions
        {
            Version = TokenizerOptions.Default.Version,
            CaseSensitive = TokenizerOptions.Default.CaseSensitive,
            TokenPatterns = new TokenPatterns
            {
                Identifier = @"(?<ig>tmp_[A-Za-z]\w*)|(?<id>[A-Za-z_]\w*)",
                Literal = TokenizerOptions.Default.TokenPatterns.Literal,
                OpenParenthesis = TokenizerOptions.Default.TokenPatterns.OpenParenthesis,
                CloseParenthesis = TokenizerOptions.Default.TokenPatterns.CloseParenthesis,
                ArgumentSeparator = TokenizerOptions.Default.TokenPatterns.ArgumentSeparator,
                Unary = TokenizerOptions.Default.TokenPatterns.Unary,
                Operators = TokenizerOptions.Default.TokenPatterns.Operators
            }
        };

        using var host = ParserApp.GetParserSessionApp<ItemParserSession>(custom);
        var session = (ParserSessionBase)host.GetParserSession();

        session.Expression = "tmp_a + keep + tmp_b2 + Other";

        var report = session.Validate(new VariableNamesOptions
        {
            KnownIdentifierNames = [ "keep" ],
            IgnoreCaptureGroups = new[] { "ig" }
        });

        var res = report.VariableNamesResult!;
        Assert.Contains("keep", res.MatchedNames);
        Assert.Contains("tmp_a", res.IgnoredNames);
        Assert.Contains("tmp_b2", res.IgnoredNames);
        Assert.Contains("Other", res.UnmatchedNames);
    }
}