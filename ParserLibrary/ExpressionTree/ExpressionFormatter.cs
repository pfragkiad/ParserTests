using System.Runtime.InteropServices;
using System.Text;

namespace ParserLibrary.ExpressionTree;

public sealed record ExpressionFormatterOptions(
    bool SpacesAroundBinaryOperators = false,
    bool SpaceAfterSeparator = false
);

public static class ExpressionFormatter
{
    public static string Format(Tree<Token> tree, TokenPatterns patterns) =>
        Format(tree, patterns, null);

    public static string Format(Tree<Token> tree, TokenPatterns patterns, ExpressionFormatterOptions? options) =>
        tree.Root is null ? string.Empty : FormatNode(tree.Root, null, false, patterns, options ?? DefaultOptions);

    public static string Format(Node<Token> node, TokenPatterns patterns, ExpressionFormatterOptions? options = null) =>
        FormatNode(node, null, false, patterns, options ?? DefaultOptions);

    private static readonly ExpressionFormatterOptions DefaultOptions = new();

    private static string FormatNode(Node<Token>? node, Operator? parentOp, bool isRightChild,
        TokenPatterns patterns, ExpressionFormatterOptions fmt)
    {
        if (node is null) return string.Empty;
        if (node.Value is not Token token) return node.Text;

        var opDict = patterns.OperatorDictionary;
        var unaryDict = patterns.UnaryOperatorDictionary;

        return token.TokenType switch
        {
            TokenType.Literal or TokenType.Identifier => token.Text,
            TokenType.Function => FormatFunction(node, token, patterns, fmt),
            TokenType.Operator => FormatOperator(node, token, parentOp, isRightChild, patterns, opDict, unaryDict, fmt),
            _ => token.Text
        };
    }

    private static string FormatFunction(Node<Token> node, Token token, TokenPatterns patterns, ExpressionFormatterOptions fmt)
    {
        // Build argument list from the right-leaning separator chain
        var argNodes = node.GetFunctionArgumentNodes(patterns.ArgumentSeparatorOperator.Name);
        var args = argNodes.Select(a => FormatNode(a, null, false, patterns, fmt));

        // Apply optional space after the separator
        string sep = fmt.SpaceAfterSeparator
            ? $"{patterns.ArgumentSeparator} "
            : patterns.ArgumentSeparator.ToString();

        return $"{token.Text}{patterns.OpenParenthesis}{string.Join(sep, args)}{patterns.CloseParenthesis}";
    }

    private static string FormatOperator(
        Node<Token> node,
        Token token,
        Operator? parentOp,
        bool isRightChild,
        TokenPatterns patterns,
        Dictionary<string, Operator> opDict,
        Dictionary<string, UnaryOperator> unaryDict,
        ExpressionFormatterOptions fmt)
    {
        bool hasLeft = node.Left is Node<Token>;
        bool hasRight = node.Right is Node<Token>;
        bool isUnary = hasLeft ^ hasRight;

        if (isUnary && unaryDict.TryGetValue(token.Text, out var uop))
        {
            var child = (hasLeft ? (Node<Token>)node.Left! : (Node<Token>)node.Right!)!;
            var childExpr = FormatNode(child, null, false, patterns, fmt);
            if (child.Value is Token ct && (ct.TokenType == TokenType.Operator || ct.TokenType == TokenType.Function))
                childExpr = $"({childExpr})";
            // No spaces for unary
            return uop.Prefix ? $"{uop.Name}{childExpr}" : $"{childExpr}{uop.Name}";
        }

        if (hasLeft && hasRight && opDict.TryGetValue(token.Text, out var bop))
        {
            var leftNode = (Node<Token>)node.Left!;
            var rightNode = (Node<Token>)node.Right!;

            var leftExpr = FormatNode(leftNode, bop, false, patterns, fmt);
            var rightExpr = FormatNode(rightNode, bop, true, patterns, fmt);

            if (NeedsChildParens(leftNode, bop, false, opDict)) leftExpr = $"({leftExpr})";
            if (NeedsChildParens(rightNode, bop, true, opDict)) rightExpr = $"({rightExpr})";

            bool needSelfParens = parentOp != null && NeedsParentParens(bop, parentOp, isRightChild);

            //we prevent spaces if the operator is the period independent of the user selection
            string opSep = fmt.SpacesAroundBinaryOperators && bop.Name != "." ? " " : "";
            var combined = $"{leftExpr}{opSep}{bop.Name}{opSep}{rightExpr}";
            return needSelfParens ? $"({combined})" : combined;
        }
        // Fallbacks: keep legacy behavior (no spacing)
        if (hasLeft && !hasRight) return $"{FormatNode((Node<Token>)node.Left!, null, false, patterns, fmt)}{token.Text}";
        if (!hasLeft && hasRight) return $"{token.Text}{FormatNode((Node<Token>)node.Right!, null, false, patterns, fmt)}";
        return token.Text;
    }

    private static bool NeedsChildParens(Node<Token> child, Operator parent, bool isRight, Dictionary<string, Operator> opDict)
    {
        if (child.Value is not Token t || t.TokenType != TokenType.Operator) return false;
        if (!opDict.TryGetValue(t.Text, out var childOp)) return false;

        if (childOp.Priority < parent.Priority) return true;
        if (childOp.Priority > parent.Priority) return false;

        if (childOp.Name == parent.Name)
        {
            if (!IsAssociative(parent.Name) && isRight) return true;
            return false;
        }
        return false;
    }

    private static bool NeedsParentParens(Operator current, Operator parent, bool isRightChild)
    {
        if (current.Priority < parent.Priority) return true;
        if (current.Priority > parent.Priority) return false;

        if (current.Name == parent.Name)
        {
            if (!IsAssociative(current.Name) && isRightChild) return true;
            return false;
        }

        if (!parent.LeftToRight && !isRightChild) return true;
        return false;
    }

    private static bool IsAssociative(string opName) =>
        opName is "+" or "*" or "&&" or "||";
}