namespace ParserLibrary.Parsers;

public partial class ParserBase
{
    /// <summary>
    /// Optimizes an expression by building its tree and applying a type-informed commutative reordering.
    /// Uses runtime variables for type inference.
    /// </summary>
    public TreeOptimizerResult GetOptimizedTree(
        string expression,
        Dictionary<string, object?>? variables = null)
    {
        var tree = GetExpressionTree(expression);
        return GetOptimizedTree(tree, variables, cloneTree:false);
    }

    /// <summary>
    /// Optimizes an existing TokenTree using the parser's runtime type inference (variables map).
    /// Performs in-place operator reuse; no NodeDictionary rebuild required.
    /// </summary>
    public TreeOptimizerResult GetOptimizedTree(
        TokenTree tree,
        Dictionary<string, object?>? variables = null,
        bool cloneTree = false)
    {
        var typeMap = InferNodeTypes(tree, variables);

        int before = CountMixed(tree.Root, typeMap);

        if (cloneTree)
        {
            var cloned = (TokenTree)tree.DeepClone();
            OptimizeNode(cloned.Root, typeMap);
            //change local reference to cloned tree
            tree = cloned;
        }
        else OptimizeNode(tree.Root, typeMap);

        var newTypeMap = InferNodeTypes(tree, variables);
        int after = CountMixed(tree.Root, newTypeMap);

        return new TreeOptimizerResult
        {
            Tree = tree,
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


    /// <summary>
    /// Infers (and caches) the Type of every node in an existing expression tree using the parser's
    /// standard Evaluate* virtual methods. Returned dictionary maps each Node to its resolved Type (null if unknown).
    /// </summary>
    /// <param name="tree">Already built expression tree</param>
    /// <param name="variables">Optional variable instances (or Types) for identifiers</param>
    protected internal Dictionary<Node<Token>, Type?> InferNodeTypes(
        TokenTree tree,
        Dictionary<string, object?>? variables = null)
    {
        // Merge constants (consistent with EvaluateType)
        variables = MergeVariableConstants(variables);

        // We reuse the existing tree nodes; we only need a postfix walk to ensure children first.
        var postfixTokens = tree.GetPostfixTokens();
        // Token -> Node lookup is in tree.NodeDictionary already.

        Dictionary<Node<Token>, Type?> nodeTypeMap = [];

        // Local helper: get node for token
        Node<Token> GetNode(Token t) => tree.NodeDictionary[t];

        // For building "argument value dictionary" similar to EvaluateType (values here are Types)
        Dictionary<Node<Token>, object?> boxedMap = new();

        foreach (var token in postfixTokens)
        {
            var node = GetNode(token);

            switch (token.TokenType)
            {
                case TokenType.Literal:
                    {
                        var t = EvaluateLiteralType(token.Text, token.CaptureGroup);
                        nodeTypeMap[node] = t;
                        boxedMap[node] = t;
                        break;
                    }
                case TokenType.Identifier:
                    {
                        Type? t = null;
                        if (variables is not null && variables.TryGetValue(token.Text, out var v))
                        {
                            if (v is Type vt) t = vt;
                            else t = v?.GetType();
                        }
                        nodeTypeMap[node] = t;
                        boxedMap[node] = t;
                        break;
                    }
                case TokenType.Function:
                    {
                        // Arguments have already been processed (postfix)
                        var args = node.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, boxedMap);
                        var ft = EvaluateFunctionType(token.Text, args);
                        nodeTypeMap[node] = ft;
                        boxedMap[node] = ft;
                        break;
                    }
                case TokenType.Operator:
                    {
                        var (lNode, rNode) = node.GetBinaryArgumentNodes();
                        var lt = nodeTypeMap.GetValueOrDefault(lNode);
                        var rt = nodeTypeMap.GetValueOrDefault(rNode);
                        Type? result = null;
                        try
                        {
                            result = EvaluateOperatorType(token.Text, lt, rt);
                        }
                        catch
                        {
                            // Unknown operator type -> leave null
                        }
                        nodeTypeMap[node] = result;
                        boxedMap[node] = result;
                        break;
                    }
                case TokenType.OperatorUnary:
                    {
                        bool isPrefix = _options.TokenPatterns.UnaryOperatorDictionary[token.Text].Prefix;
                        var child = node.GetUnaryArgumentNode(isPrefix);
                        var ct = nodeTypeMap.GetValueOrDefault(child);
                        Type? result = null;
                        try
                        {
                            result = EvaluateUnaryOperatorType(token.Text, ct);
                        }
                        catch
                        {
                            // Leave null
                        }
                        nodeTypeMap[node] = result;
                        boxedMap[node] = result;
                        break;
                    }
                case TokenType.ArgumentSeparator:
                    {
                        // Treat argument separator nodes as producing no type
                        nodeTypeMap[node] = null;
                        boxedMap[node] = null;
                        break;
                    }
                default:
                    nodeTypeMap[node] = null;
                    boxedMap[node] = null;
                    break;
            }
        }

        return nodeTypeMap;
    }

}