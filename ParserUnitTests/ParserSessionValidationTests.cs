using Microsoft.Extensions.Hosting;
using ParserLibrary;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using ParserTests.Common.Parsers;
using ParserLibrary.Parsers.Validation;
using Xunit;
using System.Text.RegularExpressions; // added

namespace ParserUnitTests;

public sealed class ItemSessionFixture : IDisposable
{
    public IHost Host { get; }

    public ItemSessionFixture()
    {
        // Single host for all tests in this class; dispose once when the fixture is torn down.
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

        var result = session.ValidateParentheses();

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.GetValidationFailures());
    }

    [Fact]
    public void CheckVariableNames_PartialMatch_UnmatchedDetected()
    {
        var session = GetItemSession();
        session.Expression = "a + b + c";

        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "a", "b" ]
        };
        var failures = session.Validate(opts);
        Assert.NotEmpty(failures);

        var vn = session.CheckVariableNames(opts);
        Assert.False(vn.IsSuccess);
        Assert.Contains("c", vn.UnmatchedNames);
    }

    [Fact]
    public void CheckFunctionNames_UnmatchedDetected()
    {
        var session = GetItemSession();
        session.Expression = "foo(1) + add(b,4)";

        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = ["b"]
        };
        _ = session.Validate(opts);

        var fn = session.CheckFunctionNames();
        Assert.False(fn.IsSuccess);
        Assert.Contains("foo", fn.UnmatchedNames);
        Assert.Contains("add", fn.MatchedNames);
    }

    [Fact]
    public void CheckEmptyFunctionArguments_Detected_ForTrailingOrLeadingEmpty()
    {
        var session = GetItemSession();

        session.Expression = "add(,4)";
        var opts = new VariableNamesOptions { KnownIdentifierNames = new HashSet<string>() };
        _ = session.Validate(opts);
        var empty1 = session.CheckEmptyFunctionArguments();
        Assert.False(empty1.IsSuccess);

        session.Expression = "add(4,)";
        _ = session.Validate(opts);
        var empty2 = session.CheckEmptyFunctionArguments();
        Assert.False(empty2.IsSuccess);
    }

    [Fact]
    public void CheckFunctionArgumentsCount_Invalid_ForTooFewArgs()
    {
        var session = GetItemSession();
        session.Expression = "add(1)";

        var opts = new VariableNamesOptions { KnownIdentifierNames = new HashSet<string>() };
        _ = session.Validate(opts);

        var argCount = session.CheckFunctionArgumentsCount();
        Assert.False(argCount.IsSuccess);
        Assert.NotEmpty(argCount.InvalidFunctions);
    }

    [Fact]
    public void CheckOperatorOperands_Succeeds_OnValidExpression()
    {
        var session = GetItemSession();
        session.Expression = "a + 1";

        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "a" ]
        };
        _ = session.Validate(opts);

        var op = session.CheckOperators();
        Assert.True(op.IsSuccess);
        Assert.Empty(op.InvalidOperators);
    }

    [Fact]
    public void CheckOrphanArgumentSeparators_Detected()
    {
        var session = GetItemSession();
        session.Expression = "a, b";
        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = ["a", "b" ]
        };

        _ = session.Validate(opts);
        var sep = session.CheckOrphanArgumentSeparators();
        Assert.False(sep.IsSuccess);
        Assert.NotEmpty(sep.InvalidPositions);
    }

    [Fact]
    public void Validate_Aggregate_RespectsProvidedKnownNames_AndEarlyReturn()
    {
        var session = GetItemSession();

        session.Expression = "(a + b";
        var opts1 = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "a", "b" ]
        };
        var failuresParen = session.Validate(opts1, earlyReturnOnErrors: true);
        Assert.NotEmpty(failuresParen);

        session.Expression = "a + b";
        session.Variables = new() { { "a", 1 }, { "b", 2 } };

        var opts2 = new VariableNamesOptions
        {
            KnownIdentifierNames =[ "a" ]
        };
        var failuresVars = session.Validate(opts2, earlyReturnOnErrors: false);
        Assert.NotEmpty(failuresVars);
    }

    // ----------------------- VariableNamesOptions exhaustive tests -----------------------

    [Fact]
    public void VariableNames_StrictMatching_NoIgnores()
    {
        var session = GetItemSession();
        session.Expression = "a + b + c";

        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "a", "c" ],
            IgnoreCaptureGroups = null,
            IgnoreIdentifierPattern = null,
            IgnorePrefixes = null,
            IgnorePostfixes = null
        };

        _ = session.Validate(opts);
        var res = session.CheckVariableNames(opts);

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

        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "keep" ],
            IgnorePrefixes = [ "pre_" ],
            IgnorePostfixes = [ "_suf", "X"]
        };

        _ = session.Validate(opts);
        var res = session.CheckVariableNames(opts);

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
        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "keep" ],
            IgnoreIdentifierPattern = pattern
        };

        _ = session.Validate(opts);
        var res = session.CheckVariableNames(opts);

        Assert.Contains("keep", res.MatchedNames);
        Assert.Contains("tmp_a", res.IgnoredNames);
        Assert.Contains("TMP", res.IgnoredNames);
        // TmpVar does not match ^[A-Z]+$ and doesn't start with tmp_, so should be unmatched
        Assert.Contains("TmpVar", res.UnmatchedNames);
    }

    [Fact]
    public void VariableNames_IgnoreCaptureGroups_CustomTokenizer()
    {
        // Build a custom tokenizer that marks tmp_* via a capture group named "ig"
        var custom = TokenizerOptions.Default;
        custom = new TokenizerOptions
        {
            Version = custom.Version,
            CaseSensitive = custom.CaseSensitive,
            TokenPatterns = new TokenPatterns
            {
                Identifier = @"(?<ig>tmp_[A-Za-z]\w*)|(?<id>[A-Za-z_]\w*)",
                Literal = custom.TokenPatterns.Literal,
                OpenParenthesis = custom.TokenPatterns.OpenParenthesis,
                CloseParenthesis = custom.TokenPatterns.CloseParenthesis,
                ArgumentSeparator = custom.TokenPatterns.ArgumentSeparator,
                Unary = custom.TokenPatterns.Unary,
                Operators = custom.TokenPatterns.Operators
            }
        };

        using var host = ParserApp.GetParserSessionApp<ItemParserSession>(custom);
        var session = (ParserSessionBase)host.GetParserSession();

        session.Expression = "tmp_a + keep + tmp_b2 + Other";

        var opts = new VariableNamesOptions
        {
            KnownIdentifierNames = [ "keep" ],
            IgnoreCaptureGroups = ["ig" ]
        };

        _ = session.Validate(opts);
        var res = session.CheckVariableNames(opts);

        Assert.Contains("keep", res.MatchedNames);
        Assert.Contains("tmp_a", res.IgnoredNames);
        Assert.Contains("tmp_b2", res.IgnoredNames);
        Assert.Contains("Other", res.UnmatchedNames);
    }
}