using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLibrary.Parsers;

public partial class ParserBase
{
    #region Expression expansion (CustomFunctions -> inline body)

    /// <summary>
    /// Returns the expression string after expanding all CustomFunctions inline.
    /// </summary>
    public string GetExpandedExpressionString(string expression, bool spacesAroundOperators = true, int maxDepth = 10)
    {
        var tree = GetExpressionTree(expression);
        var expanded = ExpandCustomFunctions(tree, maxDepth-1);
        return expanded.GetExpressionString(_options.TokenPatterns, spacesAroundOperators);
    }

    /// <summary>
    /// Returns the expression string after expanding all CustomFunctions inline for the given tree.
    /// </summary>
    public string GetExpandedExpressionString(TokenTree tree, bool spacesAroundOperators = true, int maxDepth = 10)
    {
        var expanded = ExpandCustomFunctions(tree, maxDepth-1);
        return expanded.GetExpressionString(_options.TokenPatterns, spacesAroundOperators);
    }

    /// <summary>
    /// Create a new TokenTree with CustomFunctions inlined by replacing each function node
    /// with its body where parameters are substituted by the actual argument subtrees (recursively).
    /// </summary>
    public TokenTree ExpandCustomFunctions(TokenTree tree, int maxDepth = 10)
    {
        if (tree.Root?.Value is not Token) return tree;

        // Work on a deep copy so we don't mutate caller's tree
        var working = tree.DeepCloneTyped();
        var newRoot = ExpandNode((Node<Token>)working.Root, 0, _options.TokenPatterns, maxDepth);
        // Rebuild dictionary from the new root
        var newDict = BuildNodeDictionary(newRoot);
        return new TokenTree { Root = newRoot, NodeDictionary = newDict };
    }

    private Node<Token> ExpandNode(Node<Token> node, int depth, TokenPatterns patterns, int maxDepth)
    {
        // Depth guard to prevent infinite recursion on self-recursive functions
        if (depth > maxDepth) return node;

        var tok = (Token)node.Value!;

        // Handle custom functions: inline body with parameter substitution
        if (tok.TokenType == TokenType.Function)
        {
            var functionName = patterns.CaseSensitive ? tok.Text : tok.Text.ToLower();

            if (CustomFunctions.TryGetValue(functionName, out var def))
            {
                // 1) Expand arguments first (recursive)
                var argNodes = node.GetFunctionArgumentNodes(patterns.ArgumentSeparatorOperator.Name);
                for (int i = 0; i < argNodes.Length; i++)
                {
                    argNodes[i] = ExpandNode(argNodes[i], depth + 1, patterns, maxDepth);
                }

                // 2) Build the body tree
                var bodyPostfix = GetPostfixTokens(def.Body);
                var bodyTree = GetExpressionTree(bodyPostfix);
                var bodyRoot = (Node<Token>)bodyTree.Root;

                // 3) Substitute each parameter (identifiers only) with the corresponding argument subtree
                for (int i = 0; i < def.Parameters.Length; i++)
                {
                    string paramName = def.Parameters[i];
                    // IMPORTANT: produce a fresh clone with unique Token instances for every occurrence
                    Node<Token> ReplacementFactory() => CloneSubtree(argNodes[i]);
                    bodyRoot = ReplaceAllIdentifiers(bodyRoot, paramName, ReplacementFactory, patterns.CaseSensitive);
                }

                // 4) Recursively expand nested custom functions in the substituted body
                bodyRoot = ExpandNode(bodyRoot, depth + 1, patterns, maxDepth);

                return bodyRoot;
            }

            // Non-custom function: expand arguments only
            if (node.Right is Node<Token> rightFuncArgs)
                node.Right = ExpandNode(rightFuncArgs, depth + 1, patterns, maxDepth);

            return node;
        }

        // Recurse for operators/unary and any other constructs
        if (node.Left is Node<Token> left)
            node.Left = ExpandNode(left, depth + 1, patterns, maxDepth);
        if (node.Right is Node<Token> right)
            node.Right = ExpandNode(right, depth + 1, patterns, maxDepth);

        return node;
    }

    /// <summary>
    /// Replace all Identifier nodes equal to 'identifierName' with a deep clone produced by replacementFactory.
    /// Returns the possibly new root.
    /// </summary>
    private static Node<Token> ReplaceAllIdentifiers(
        Node<Token> root,
        string identifierName,
        Func<Node<Token>> replacementFactory,
        bool caseSensitive)
    {
        bool EqualsName(string s1, string s2) =>
            caseSensitive ? string.Equals(s1, s2, StringComparison.Ordinal)
                          : string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

        Node<Token> Recurse(Node<Token> current)
        {
            var t = (Token)current.Value!;
            if (t.TokenType == TokenType.Identifier && EqualsName(t.Text, identifierName))
            {
                // Return a fresh clone for this occurrence
                return replacementFactory();
            }

            if (current.Left is Node<Token> l)
                current.Left = Recurse(l);
            if (current.Right is Node<Token> r)
                current.Right = Recurse(r);

            return current;
        }

        return Recurse(root);
    }

    /// <summary>
    /// Deep clone a subtree of Node&lt;Token&gt;.
    /// Tokens are ALSO cloned to guarantee unique Token instances per node.
    /// This keeps the Token->Node dictionary consistent after expansion.
    /// </summary>
    private static Node<Token> CloneSubtree(Node<Token> node)
    {
        var clonedToken = ((Token)node.Value!).Clone(); // deep clone token to avoid shared references
        var cloned = new Node<Token>(clonedToken);
        if (node.Left is Node<Token> l) cloned.Left = CloneSubtree(l);
        if (node.Right is Node<Token> r) cloned.Right = CloneSubtree(r);
        return cloned;
    }

    /// <summary>
    /// Build a fresh Token->Node dictionary by traversing from the provided root.
    /// </summary>
    private static Dictionary<Token, Node<Token>> BuildNodeDictionary(Node<Token> root)
    {
        var dict = new Dictionary<Token, Node<Token>>();
        var stack = new Stack<Node<Token>>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var n = stack.Pop();
            var tok = (Token)n.Value!;
            if (!dict.ContainsKey(tok))
                dict.Add(tok, n);

            if (n.Left is Node<Token> l) stack.Push(l);
            if (n.Right is Node<Token> r) stack.Push(r);
        }

        return dict;
    }

    #endregion
}
