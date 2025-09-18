using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Validation;
using Xunit;

namespace ParserUnitTests;

public class ParserSession_TokenizerUnaryEdgeTests : IClassFixture<ItemSessionFixture>
{
    private readonly ItemSessionFixture _fixture;
    public ParserSession_TokenizerUnaryEdgeTests(ItemSessionFixture fixture) => _fixture = fixture;
    private ParserSessionBase GetItemSession() => _fixture.CreateSession();

    [Fact]
    public void PrefixUnaryAtEnd_ValidationFails_NoCrash()
    {
        var session = GetItemSession();
        session.Expression = "-";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.UnaryOperatorOperandsResult!.IsSuccess);
    }

    [Fact]
    public void PrefixUnaryInsideParentheses_ValidationFails_NoCrash()
    {
        var session = GetItemSession();
        session.Expression = "(-)";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.UnaryOperatorOperandsResult!.IsSuccess);
    }

    [Fact]
    public void PostfixUnaryAtEnd_ValidationPasses()
    {
        var session = GetItemSession();
        session.Expression = "5%"; // '%' is postfix-unary by default

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);
        Assert.True(report.IsSuccess);
        Assert.True(report.UnaryOperatorOperandsResult!.IsSuccess);
    }

    [Fact]
    public void TrailingBinaryOperator_ValidationFails_InvalidOperatorOperands()
    {
        var session = GetItemSession();
        session.Expression = "1 +";

        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);

        Assert.False(report.IsSuccess);
        Assert.False(report.BinaryOperatorOperandsResult!.IsSuccess);
    }

    // --- Additional tests for ambiguous postfix/binary/unary ---

    [Theory]
    [InlineData("a%+b", true)] // postfix then binary
    [InlineData("a%b", false)] // postfix then identifier (should fail)
    [InlineData("a%+", false)] // postfix then binary at end (should fail)
    [InlineData("(a%)", true)] // postfix inside parens
    [InlineData("a%", true)] // postfix at end
    [InlineData("a%-b", true)] // postfix then binary
    [InlineData("a-+b", true)] // binary then unary
    [InlineData("a--b", true)] // binary then unary
    [InlineData("a-!b", true)] // binary then unary
    [InlineData("a%%", true)] // postfix then postfix
    public void AmbiguousPostfixAndBinaryOperatorCases(string expr, bool shouldSucceed)
    {
        var session = GetItemSession();
        session.Expression = expr;
        var report = session.ValidateAndCompile(new VariableNamesOptions()
        { KnownIdentifierNames = ["a", "b"] });


        if (shouldSucceed)
        {
            Assert.True(report.IsSuccess);
            Assert.True(report.UnaryOperatorOperandsResult == null || report.UnaryOperatorOperandsResult.IsSuccess);
            Assert.True(report.BinaryOperatorOperandsResult == null || report.BinaryOperatorOperandsResult.IsSuccess);
        }
        else
        {
            Assert.False(report.IsSuccess);
        }
    }

    [Fact]
    public void PostfixBeforeParenthesis_Invalid()
    {
        var session = GetItemSession();
        session.Expression = "a%()";  //also try a+!
        var report = session.ValidateAndCompile(new VariableNamesOptions()
        { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.True(report.UnexpectedOperatorOperandsResult != null && !report.UnexpectedOperatorOperandsResult.IsSuccess);
    }


    [Fact]
    public void PrefixAfterParenthesis_Invalid()
    {
        var session = GetItemSession();
        session.Expression = "(a)!";  //also try a+!
        var report = session.ValidateAndCompile( new VariableNamesOptions()
        { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.True(report.UnexpectedOperatorOperandsResult != null && !report.UnexpectedOperatorOperandsResult.IsSuccess);
    }

    [Fact]
    public void PrefixAfterOp_Invalid()
    {
        var session = GetItemSession();
        session.Expression = "a+!";  //also try a+!
        var report = session.ValidateAndCompile(new VariableNamesOptions()
        { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.True(report.UnaryOperatorOperandsResult != null && !report.UnaryOperatorOperandsResult.IsSuccess);
    }

    [Fact]
    public void PrefixAfterOperand_Invalid()
    {
        var session = GetItemSession();
        session.Expression = "a!";  //also try a+!
        var report = session.ValidateAndCompile(new VariableNamesOptions()
        { KnownIdentifierNames = ["a"] });

        Assert.False(report.IsSuccess);
        Assert.True(report.UnexpectedOperatorOperandsResult != null && !report.UnexpectedOperatorOperandsResult.IsSuccess);
    }


    [Fact]
    public void PostfixAfterOperator_Invalid()
    {
        var session = GetItemSession();
        session.Expression = "+!";
        var report = session.ValidateAndCompile(VariableNamesOptions.Empty);
        Assert.False(report.IsSuccess);
    }
}