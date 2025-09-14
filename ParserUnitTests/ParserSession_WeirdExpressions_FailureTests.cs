using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Validation;
using Xunit;

namespace ParserUnitTests;

public class ParserSession_WeirdExpressions_FailureTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_WeirdExpressions_FailureTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void EmptyExpression_SucceedsValidation()
    {
        var session = GetItemSession();
        session.Expression = "";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.True(report.IsSuccess);
    }

    [Fact]
    public void SpacesOnlyExpression_SucceedsValidation()
    {
        var session = GetItemSession();
        session.Expression = "   \t  \r\n ";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.True(report.IsSuccess);
    }

    [Fact]
    public void UnknownSymbolToken_FailsValidation()
    {
        var session = GetItemSession();
        session.Expression = "1 + $";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
    }

    [Fact]
    public void LeadingComma_FailsOrphanSeparators()
    {
        var session = GetItemSession();
        session.Expression = ",a";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.OrphanArgumentSeparatorsResult!.IsSuccess);
        Assert.NotEmpty(report.OrphanArgumentSeparatorsResult.GetValidationFailures());
    }

    [Fact]
    public void TrailingCommaOnly_FailsOrphanSeparators()
    {
        var session = GetItemSession();
        session.Expression = ",";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.OrphanArgumentSeparatorsResult!.IsSuccess);
        Assert.NotEmpty(report.OrphanArgumentSeparatorsResult.GetValidationFailures());
    }

    [Fact]
    public void DoubleCommaBetweenIdentifiers_FailsOrphanSeparators()
    {
        var session = GetItemSession();
        session.Expression = "a,,b";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.OrphanArgumentSeparatorsResult!.IsSuccess);
        Assert.NotEmpty(report.OrphanArgumentSeparatorsResult.GetValidationFailures());
    }

    [Fact]
    public void BinaryOperatorAtStart_FailsBinaryOperands()
    {
        var session = GetItemSession();
        session.Expression = "*1";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.BinaryOperatorOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.BinaryOperatorOperandsResult.InvalidOperators);
    }

    [Fact]
    public void AdjacentBinaryOperators_FailsAdjacentOperands()
    {
        var session = GetItemSession();
        session.Expression = "1 + * 2";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
    }


    [Fact]
    public void OnlyBinaryOperator_Sucess()
    {
        var session = GetItemSession();
        session.Expression = "-----1";
        var report = session.Validate(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);
        Assert.Equal(-1, session.Evaluate());
    }

    [Fact]
    public void ChainedPostfixUnary_SuccessUnaryOperands()
    {
        var session = GetItemSession();
        // '%' is postfix-unary by default (see existing tests)
        session.Expression = "5%%";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.True(report.IsSuccess);
    }

    [Fact]
    public void EmptyGroupingParentheses_Succeeds()
    {
        var session = GetItemSession();
        session.Expression = "()";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.True(report.IsSuccess);

        //assert that this result is null
        var result = session.Evaluate();
        Assert.Null(result);
    }

    [Fact]
    public void ConsecutiveGroupsWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "(1)(2)";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void ConsecutiveLiteralsWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "1 2";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void ConsecutiveVariablesWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "a b";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void LiteralThenVariableWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "1 a";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void VariableThenLiteralWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "a 1";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void VariableThenFunctionWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "a tre()";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void LiteralThenFunctionWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "1 tre()";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void VariableThenOpenParenGroupWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "a (b)";

        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.FunctionNamesResult!.IsSuccess);
    }

    [Fact]
    public void LiteralThenOpenParenGroupWithoutOperator_Fails()
    {
        var session = GetItemSession();
        session.Expression = "1 (2)";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
        Assert.NotEmpty(report.AdjacentOperandsResult.GetValidationFailures());
    }

    [Fact]
    public void VariableInvokedAsFunction_FailsFunctionNames()
    {
        var session = GetItemSession();
        session.Expression = "a(1) + b";
        // Both identifiers are known variables; 'a(1)' should be treated as a function call to unknown 'a'
        var report = session.Validate(new VariableNamesOptions { KnownIdentifierNames = ["a", "b"] });

        Assert.False(report.IsSuccess);
        Assert.False(report.FunctionNamesResult!.IsSuccess);
        Assert.Contains("a", report.FunctionNamesResult.UnmatchedNames);
        // AdjacentOperands may also flag here depending on tokenizer output; do not assert it strictly.
    }

    [Fact]
    public void MissingArgumentSeparatorInsideFunction_Fails()
    {
        var session = GetItemSession();
        session.Expression = "add(1 2)";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
    }

    [Fact]
    public void FunctionWithTrailingEmptyArg_FailsEmptyArgs_And_Count()
    {
        var session = GetItemSession();
        session.Expression = "add(1,)";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.EmptyFunctionArgumentsResult!.IsSuccess);
    }

    [Fact]
    public void MultipleEmptyArgs_FailsEmptyArgs_And_Count()
    {
        var session = GetItemSession();
        session.Expression = "add(, ,)";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.EmptyFunctionArgumentsResult!.IsSuccess);
        Assert.False(report.FunctionArgumentsCountResult!.IsSuccess);
    }

    [Fact]
    public void UnaryFollowedByBinaryWithoutOperand_Fails()
    {
        var session = GetItemSession();
        session.Expression = "-*1";

        var report = session.Validate(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.AdjacentOperandsResult!.IsSuccess);
    }
}