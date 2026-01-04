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
            TokenType.OperatorUnary or  TokenType.Operator => FormatOperator(node, token, parentOp, isRightChild, patterns, opDict, unaryDict, fmt),
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
            // Only associative operators can safely omit parens
            if (!IsAssociative(parent.Name) && isRight) return true;
            return false;
        }
        return true;
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
        opName is "+" or "*" or "&&" or "||" or "&" or "|" or "^";
    public static string Format(List<Token> infixTokens, TokenPatterns patterns, ExpressionFormatterOptions? options)
    {
        if (infixTokens is null || infixTokens.Count == 0) return string.Empty;

        var fmt = options ?? DefaultOptions;
        var sb = new StringBuilder(infixTokens.Count * 2);

        var opDict = patterns.OperatorDictionary;
        var unaryDict = patterns.UnaryOperatorDictionary;
        var prefixUnary = patterns.PrefixUnaryNames;
        var postfixUnary = patterns.PostfixUnaryNames;

        static bool IsSingleChar(Token t, char c) => t.Text.Length == 1 && t.Text[0] == c;

        bool IsOpenParen(Token t) => IsSingleChar(t, patterns.OpenParenthesis);
        bool IsCloseParen(Token t) => IsSingleChar(t, patterns.CloseParenthesis);
        bool IsSeparator(Token t) => IsSingleChar(t, patterns.ArgumentSeparator);

        static Token? PrevNonNull(IReadOnlyList<Token> list, int i)
        {
            for (int p = i - 1; p >= 0; p--)
            {
                if (!list[p].IsNull) return list[p];
            }
            return null;
        }

        static Token? NextNonNull(IReadOnlyList<Token> list, int i)
        {
            for (int n = i + 1; n < list.Count; n++)
            {
                if (!list[n].IsNull) return list[n];
            }
            return null;
        }

        static bool IsOperandish(Token? t, Func<Token, bool> isCloseParen)
        {
            if (t is null) return false;
            if (isCloseParen(t)) return true;
            return t.TokenType is TokenType.Identifier or TokenType.Literal or TokenType.Function;
        }

        static bool IsOperatorish(Token? t, Func<Token, bool> isOpenParen, Func<Token, bool> isSeparator)
        {
            if (t is null) return true;
            if (isOpenParen(t) || isSeparator(t)) return true;
            return t.TokenType == TokenType.Operator;
        }

        void TrimEndSpace()
        {
            if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
        }

        for (int i = 0; i < infixTokens.Count; i++)
        {
            var tok = infixTokens[i];
            if (tok.IsNull) continue;

            string text = tok.Text;

            bool openParen = IsOpenParen(tok);
            bool closeParen = IsCloseParen(tok);
            bool separator = IsSeparator(tok);

            bool isOperatorToken = tok.TokenType == TokenType.Operator && !openParen && !closeParen && !separator;

            if (separator)
            {
                TrimEndSpace();
                sb.Append(patterns.ArgumentSeparator);
                if (fmt.SpaceAfterSeparator) sb.Append(' ');
                continue;
            }

            if (openParen)
            {
                TrimEndSpace();
                sb.Append(patterns.OpenParenthesis);
                continue;
            }

            if (closeParen)
            {
                TrimEndSpace();
                sb.Append(patterns.CloseParenthesis);
                continue;
            }

            if (isOperatorToken)
            {
                var prev = PrevNonNull(infixTokens, i);
                var next = NextNonNull(infixTokens, i);

                bool nameInUnary = unaryDict.ContainsKey(text);
                bool nameInBinary = opDict.ContainsKey(text);

                bool prefix = false;
                bool postfix = false;
                bool binary = false;

                if (nameInUnary && !nameInBinary)
                {
                    // purely unary, use defined orientation
                    prefix = unaryDict[text].Prefix;
                    postfix = !prefix;
                }
                else if (!nameInUnary && nameInBinary)
                {
                    // purely binary
                    binary = true;
                }
                else
                {
                    // ambiguous or unknown: decide by context with TokenPatterns hints
                    bool prevIsOperand = IsOperandish(prev, IsCloseParen);
                    bool prevIsOperator = IsOperatorish(prev, IsOpenParen, IsSeparator);
                    bool nextIsOperand = IsOperandish(next, IsCloseParen);

                    if (!prevIsOperand && prefixUnary.Contains(text))
                    {
                        prefix = true;
                    }
                    else if (prevIsOperand && postfixUnary.Contains(text))
                    {
                        postfix = true;
                    }
                    else
                    {
                        // fallback heuristic: start or after operator/open/separator => prefix; otherwise binary
                        if (!prevIsOperand || prevIsOperator)
                        {
                            prefix = nameInUnary && unaryDict[text].Prefix;
                            if (!prefix && !nameInUnary) binary = true; // if not known unary, treat as binary
                        }
                        else
                        {
                            // after an operand
                            if (postfixUnary.Contains(text))
                            {
                                postfix = true;
                            }
                            else
                            {
                                binary = true;
                            }
                        }
                    }
                }

                // Special-case dot/member access: never add spaces
                bool isDot = text == ".";
                if (isDot)
                {
                    TrimEndSpace();
                    sb.Append('.');
                    continue;
                }

                if (prefix || postfix)
                {
                    // No spaces for unary (both prefix and postfix)
                    TrimEndSpace();
                    sb.Append(text);
                    continue;
                }

                // Binary formatting
                if (binary || nameInBinary)
                {
                    if (fmt.SpacesAroundBinaryOperators)
                    {
                        TrimEndSpace();
                        if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                        sb.Append(text);
                        sb.Append(' ');
                    }
                    else
                    {
                        TrimEndSpace();
                        sb.Append(text);
                    }
                    continue;
                }

                // Unknown operator: be conservative, no extra spaces
                TrimEndSpace();
                sb.Append(text);
                continue;
            }

            // Function names: emit as-is; '(' will follow and we don't add a space before it.
            if (tok.TokenType == TokenType.Function)
            {
                bool needSpaceBefore = sb.Length > 0 && (char.IsLetterOrDigit(sb[^1]) || sb[^1] == patterns.CloseParenthesis);
                if (needSpaceBefore) sb.Append(' ');
                sb.Append(text);
                continue;
            }

            // Identifiers and Literals
            if (tok.TokenType is TokenType.Identifier or TokenType.Literal)
            {
                bool needSpaceBefore = sb.Length > 0 && (char.IsLetterOrDigit(sb[^1]) || sb[^1] == patterns.CloseParenthesis);
                if (needSpaceBefore) sb.Append(' ');
                sb.Append(text);
                continue;
            }

            // Fallback: append raw text
            sb.Append(text);
        }

        return sb.ToString();
    }
}