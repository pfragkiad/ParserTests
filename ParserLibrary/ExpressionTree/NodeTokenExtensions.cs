using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public static class NodeTokenExtensions
{
    // Count args by token type (ArgumentSeparator), no separator parameter needed
    public static int GetFunctionArgumentsCount(this Node<Token> node)
    {
        if (node.Left is not null)
            throw new InvalidOperationException($"The function node '{node.Text}' cannot contain a non-null Left child node.");

        if (node.Right is null) return 0;

        // Zero-arg placeholder: Right is a Token.Null literal (Match.Empty)
        if (node.Right is Node<Token> rt && rt.Value is not null && rt.Value.IsNull)
            return 0;

        // If the right child is not a separator, we have a single argument
        if (node.Right is not Node<Token> rNode || rNode.Value is null || rNode.Value.TokenType != TokenType.ArgumentSeparator)
            return 1;

        // An argument separator exists, so we have at least 2 arguments
        int count = 2;

        NodeBase? leftNode = rNode.Left;
        while (leftNode is Node<Token> ln && ln.Value is not null)
        {
            if (ln.Value.TokenType == TokenType.ArgumentSeparator) count++;
            leftNode = ln.Left;
        }
        return count;
    }

    // Convenience: get function argument nodes without passing a separator
    public static Node<Token>[] GetFunctionArgumentNodes(this Node<Token> node)
    {
        int count = node.GetFunctionArgumentsCount();
        return node.GetFunctionArgumentNodes(count);
    }

    // Convenience: get function argument values without passing a separator
    public static object?[] GetFunctionArguments(this Node<Token> node, Dictionary<Node<Token>, object?> nodeValueDictionary)
    {
        int count = node.GetFunctionArgumentsCount();
        return node.GetFunctionArguments(count, nodeValueDictionary);
    }

    public static bool IsNullNode(this Node<Token>? node) =>
        node is null || node.Value is not null && node.Value.IsNull;

    public static bool IsNullNodeToken(this NodeBase? node)
    {
        if (node is null) return true;
        if (node is not Node<Token> tNode) return true;
        if(tNode.Value is null) return true;
        return tNode.Value!.IsNull;
    }
}