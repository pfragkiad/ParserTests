using ParserLibrary;
using ParserLibrary.ExpressionTree;
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

    [Fact]
    public void ReplaceNode_ReplacesLeafAndSubtree_AndRebuildsLookupState()
    {
        var parser = ParserApp.GetDefaultParser();
        var tree = parser.GetExpressionTree("a+b");
        tree.BuildParentMap();

        var oldRight = (Node<Token>)tree.Root.Right!;
        tree.ReplaceNode(oldRight, new Node<Token>(new Token(TokenType.Identifier, "c", 0)));

        Assert.Equal("+", tree.Root.Value!.Text);
        Assert.Equal("a", ((Node<Token>)tree.Root.Left!).Value!.Text);
        Assert.Equal("c", ((Node<Token>)tree.Root.Right!).Value!.Text);
        Assert.NotNull(tree.ParentMap);
        Assert.Same(tree.Root, tree.ParentMap![(Node<Token>)tree.Root.Right!]);
        Assert.True(tree.NodeDictionary.ContainsKey(((Node<Token>)tree.Root.Right!).Value!));
        Assert.False(tree.NodeDictionary.ContainsKey(oldRight.Value!));

        var tree2 = parser.GetExpressionTree("a+b");
        var subtree = parser.GetExpressionTree("x*y");
        tree2.ReplaceNode((Node<Token>)tree2.Root.Right!, subtree);

        Assert.Equal("+", tree2.Root.Value!.Text);
        Assert.Equal("a", ((Node<Token>)tree2.Root.Left!).Value!.Text);
        var replaced = (Node<Token>)tree2.Root.Right!;
        Assert.Equal("*", replaced.Value!.Text);
        Assert.Equal("x", ((Node<Token>)replaced.Left!).Value!.Text);
        Assert.Equal("y", ((Node<Token>)replaced.Right!).Value!.Text);
    }

    [Fact]
    public void ReplaceAllNodesAndRebuildOnce_ReplacesAllMatchingNodes()
    {
        var parser = ParserApp.GetDefaultParser();
        var tree = parser.GetExpressionTree("a+b+a");
        tree.BuildParentMap();

        int replacedCount = tree.ReplaceAllNodesAndRebuildOnce(t => t.Text == "a", new Node<Token>(new Token(TokenType.Identifier, "x", 0)));

        Assert.Equal(2, replacedCount);
        Assert.Equal("+", tree.Root.Value!.Text);
        Assert.Equal("+", ((Node<Token>)tree.Root.Left!).Value!.Text);
        Assert.Equal("x", ((Node<Token>)tree.Root.Right!).Value!.Text);
        Assert.Equal("x", ((Node<Token>)((Node<Token>)tree.Root.Left!).Left!).Value!.Text);
        Assert.Equal("b", ((Node<Token>)((Node<Token>)tree.Root.Left!).Right!).Value!.Text);
        Assert.NotNull(tree.ParentMap);
        Assert.Equal(5, tree.Count);

        var tree2 = parser.GetExpressionTree("a+b+a");
        int replacedCount2 = tree2.ReplaceAllNodesAndRebuildOnce(t => t.Text == "+", parser.GetExpressionTree("m*n"));

        Assert.Equal(2, replacedCount2);
        Assert.Equal("*", tree2.Root.Value!.Text);
        Assert.Equal("m", ((Node<Token>)tree2.Root.Left!).Value!.Text);
        Assert.Equal("n", ((Node<Token>)tree2.Root.Right!).Value!.Text);
    }
}
