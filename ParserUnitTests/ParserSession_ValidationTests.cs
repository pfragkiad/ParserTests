using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Validation;

namespace ParserUnitTests;

public class ParserSession_ValidationTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_ValidationTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void ValidateParentheses_Fails_OnUnmatched()
    {
        var session = GetItemSession();
        session.Expression = "(a + (b * 2)";

        var report = session.Validate(VariableNamesOptions.Empty, earlyReturnOnErrors: true);

        Assert.False(report.ParenthesesResult!.IsSuccess);
        Assert.NotEmpty(report.ParenthesesResult.GetValidationFailures());
    }

    [Fact]
    public void CheckVariableNames_PartialMatch_UnmatchedDetected()
    {
        var session = GetItemSession();
        session.Expression = "a + b + c";

        var opts = new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] };
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
        var report1 = session.Validate(VariableNamesOptions.Empty);
        Assert.False(report1.EmptyFunctionArgumentsResult!.IsSuccess);

        session.Expression = "add(4,)";
        var report2 = session.Validate(VariableNamesOptions.Empty);
        Assert.False(report2.EmptyFunctionArgumentsResult!.IsSuccess);
    }

    [Fact]
    public void CheckFunctionArgumentsCount_Invalid_ForTooFewArgs()
    {
        var session = GetItemSession();
        session.Expression = "add(1)";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.FunctionArgumentsCountResult!.IsSuccess);
        Assert.NotEmpty(report.FunctionArgumentsCountResult.InvalidFunctions);
    }

    [Fact]
    public void CheckOperatorOperands_Succeeds_OnValidExpression()
    {
        var session = GetItemSession();
        session.Expression = "a + 1";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.True(report.BinaryOperatorOperandsResult!.IsSuccess);
        Assert.Empty(report.BinaryOperatorOperandsResult.InvalidOperators);
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
}