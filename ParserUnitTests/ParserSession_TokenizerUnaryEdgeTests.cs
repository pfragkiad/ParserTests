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
}