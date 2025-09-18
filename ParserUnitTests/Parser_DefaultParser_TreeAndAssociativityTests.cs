using ParserLibrary;
using ParserLibrary.Tokenizers;
using Xunit;

namespace ParserUnitTests;

public class Parser_DefaultParser_TreeAndAssociativityTests
{
    [Fact]
    public void Exponentiation_IsRightAssociative()
    {
        var parser = ParserApp.GetDefaultParser();
        // 2^(3^2) = 2^9 = 512 (if left associative would be (2^3)^2 = 64)
        double v = (double)parser.Evaluate("2^3^2")!;
        Assert.Equal(512d, v, 10);

        var tree = parser.GetExpressionTree("2^3^2");
        Assert.Equal("^", tree.Root.Value!.Text);
        Assert.Equal("^", (tree.Root.Right as Node<Token>)!.Value!.Text); // Right-associative chain
    }

    [Fact]
    public void UnaryMinusAndAddition_EvaluatesCorrectly()
    {
        var parser = ParserApp.GetDefaultParser();
        double v = (double)parser.Evaluate("-5.0+4.0")!;
        Assert.Equal(-1.0, v, 12);
    }
}