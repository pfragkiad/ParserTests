using System;
using System.Collections.Generic;
using System.Linq;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

public partial class CoreParser
{
    /// <summary>
    /// Optimizes an expression by building its tree and applying a type-informed commutative reordering.
    /// Uses runtime variables for type inference.
    /// </summary>
    public TreeOptimizerResult GetOptimizedExpressionUsingParser(
        string expression,
        Dictionary<string, object?>? variables = null)
    {
        var tree = GetExpressionTree(expression);
        return OptimizeTreeUsingInference(tree, variables);
    }

    /// <summary>
    /// Optimizes an existing TokenTree using the parser's runtime type inference (variables map).
    /// Performs in-place operator reuse; no NodeDictionary rebuild required.
    /// </summary>
    public TreeOptimizerResult OptimizeTreeUsingInference(
        TokenTree tree,
        Dictionary<string, object?>? variables = null)
    {
        var typeMap = InferNodeTypes(tree, variables);

        int before = CountMixed(tree.Root, typeMap);

        var cloned = (TokenTree)tree.DeepClone();
        OptimizeNode(cloned.Root, typeMap);

        var newTypeMap = InferNodeTypes(cloned, variables);
        int after = CountMixed(cloned.Root, newTypeMap);

        return new TreeOptimizerResult
        {
            Tree = cloned,
            NonAllNumericBefore = before,
            NonAllNumericAfter = after
        };
    }

    private static void OptimizeNode(Node<Token>? node, Dictionary<Node<Token>, Type?> typeMap)
    {
        if (node is null) return;
        if (node.Left is Node<Token> l) OptimizeNode(l, typeMap);
        if (node.Right is Node<Token> r) OptimizeNode(r, typeMap);

        if (IsCommutative(node))
            ReorderInPlace(node, typeMap);
    }

    private static bool IsCommutative(Node<Token> node) =>
        node.Value is Token t &&
        t.TokenType == TokenType.Operator &&
        (t.Text == "+" || t.Text == "*");

    private static void ReorderInPlace(Node<Token> root, Dictionary<Node<Token>, Type?> typeMap)
    {
        var opText = ((Token)root.Value!).Text;

        // 1) Collect operands (left-to-right)
        var operands = new List<Node<Token>>();
        void CollectOperands(Node<Token>? n)
        {
            if (n is null) return;
            if (n.Value is Token t && t.TokenType == TokenType.Operator && t.Text == opText)
            {
                CollectOperands(n.Left as Node<Token>);
                CollectOperands(n.Right as Node<Token>);
            }
            else
            {
                operands.Add(n);
            }
        }
        CollectOperands(root);
        if (operands.Count <= 2) return;

        bool anyNumeric = operands.Any(o => IsNumeric(o, typeMap));
        bool anyNon = operands.Any(o => !IsNumeric(o, typeMap));
        if (!(anyNumeric && anyNon)) return;

        // 2) Order operands (numeric first)
        var ordered = operands
            .OrderBy(o => IsNumeric(o, typeMap) ? 0 : 1)
            .ToList();

        // 3) Collect operator nodes of the chain (post-order so the last is the root)
        var opNodes = new List<Node<Token>>();
        void CollectOpsPost(Node<Token>? n)
        {
            if (n is null) return;
            if (n.Value is Token t && t.TokenType == TokenType.Operator && t.Text == opText)
            {
                CollectOpsPost(n.Left as Node<Token>);
                CollectOpsPost(n.Right as Node<Token>);
                opNodes.Add(n);
            }
        }
        CollectOpsPost(root);

        // Sanity: operators should be operands.Count - 1
        if (opNodes.Count != ordered.Count - 1) return;

        // 4) Rewire in-place so the root of the chain remains 'root'
        var acc = ordered[0];
        for (int i = 0; i < opNodes.Count; i++)
        {
            var opNode = opNodes[i];
            opNode.Left = acc;
            opNode.Right = ordered[i + 1];
            acc = opNode;
        }
    }

    private static bool IsNumeric(Node<Token> node, Dictionary<Node<Token>, Type?> typeMap)
    {
        if (!typeMap.TryGetValue(node, out var t) || t is null) return false;
        return IsNumericType(t);
    }

    private static bool IsNumericType(Type t) =>
        t == typeof(byte) || t == typeof(sbyte) ||
        t == typeof(short) || t == typeof(ushort) ||
        t == typeof(int) || t == typeof(uint) ||
        t == typeof(long) || t == typeof(ulong) ||
        t == typeof(float) || t == typeof(double) ||
        t == typeof(decimal);

    private static int CountMixed(Node<Token>? root, Dictionary<Node<Token>, Type?> typeMap)
    {
        if (root is null) return 0;
        int count = 0;
        void Walk(Node<Token>? n)
        {
            if (n is null) return;
            if (n.Value is Token t && t.TokenType == TokenType.Operator &&
                n.Left is Node<Token> l && n.Right is Node<Token> r)
            {
                bool ln = IsNumeric(l, typeMap);
                bool rn = IsNumeric(r, typeMap);
                if (!(ln && rn)) count++;
            }
            Walk(n.Left as Node<Token>);
            Walk(n.Right as Node<Token>);
        }
        Walk(root);
        return count;
    }
}