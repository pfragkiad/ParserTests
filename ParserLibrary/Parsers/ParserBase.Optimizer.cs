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
        var expandedTree = ExpandCustomFunctions(tree, maxDepth: int.MaxValue);

        // IMPORTANT: expansion may create new nodes/tokens; ensure dictionary is consistent.
        expandedTree.RebuildNodeDictionaryFromStructure();

        tree = expandedTree;

        var typeMap = InferNodeTypes(tree, variables);

        int before = CountMixed(tree.Root, typeMap);

        if (cloneTree)
        {
            var cloned = (TokenTree)tree.DeepClone();
            OptimizeNode(cloned.Root, typeMap);

            // After in-place rewiring, rebuild dictionary to match new structure.
            cloned.RebuildNodeDictionaryFromStructure();

            tree = cloned;
        }
        else
        {
            OptimizeNode(tree.Root, typeMap);

            // After in-place rewiring, rebuild dictionary to match new structure.
            tree.RebuildNodeDictionaryFromStructure();
        }

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

        if (node.Value is not Token t || t.TokenType != TokenType.Operator) return;

        // '+' stays purely commutative
        if (t.Text == "+")
        {
            ReorderInPlace(node, typeMap);
            return;
        }

        // Normalize multiplicative chains: handle '*' and '/' together
        if (t.Text == "*" || t.Text == "/")
        {
            ReorderMulDivChainInPlace(node, typeMap);
        }
    }

    private static bool IsCommutative(Node<Token> node) =>
        node.Value is Token t &&
        t.TokenType == TokenType.Operator &&
        (t.Text == "+" || t.Text == "*");

    private static bool IsMulDiv(Node<Token> node) =>
        node.Value is Token tt &&
        tt.TokenType == TokenType.Operator &&
        (tt.Text == "*" || tt.Text == "/");

    private static void ReorderInPlace(Node<Token> root, Dictionary<Node<Token>, Type?> typeMap)
    {
        var opText = ((Token)root.Value!).Text;

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
        bool hasNumericLiteral = operands.Any(o => IsNumericLiteral(o, typeMap));

        // Proceed if:
        // - previous heuristic (mix of numeric and non-numeric), or
        // - there is at least one numeric literal (to prioritize constants even when all are numeric)
        if (!(hasNumericLiteral || (anyNumeric && anyNon))) return;

        // 2) Order operands: numeric literals first, then other numeric, then non-numeric.
        // Within the same group, preserve original relative order (OrderBy is stable).
        static int Rank(Node<Token> n, Dictionary<Node<Token>, Type?> tm)
            => IsNumericLiteral(n, tm) ? 0
             : IsNumeric(n, tm)        ? 1
             :                           2;

        var ordered = operands
            .OrderBy(o => Rank(o, typeMap))
            .ToList();

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

        if (opNodes.Count != ordered.Count - 1) return;

        var acc = ordered[0];
        for (int i = 0; i < opNodes.Count; i++)
        {
            var opNode = opNodes[i];
            opNode.Left = acc;
            opNode.Right = ordered[i + 1];
            acc = opNode;
        }
    }

    // NEW: handle multiplicative chains with '*' and '/' together.
    private static void ReorderMulDivChainInPlace(Node<Token> root, Dictionary<Node<Token>, Type?> typeMap)
    {
        if (!IsMulDiv(root)) return;

        // Collect numerator and denominator operands across the whole chain
        var numerators = new List<Node<Token>>();
        var denominators = new List<Node<Token>>();

        void Collect(Node<Token>? n, int sign)
        {
            if (n is null) return;
            if (n.Value is Token t && t.TokenType == TokenType.Operator)
            {
                if (t.Text == "*")
                {
                    Collect(n.Left as Node<Token>, sign);
                    Collect(n.Right as Node<Token>, sign);
                    return;
                }
                if (t.Text == "/")
                {
                    Collect(n.Left as Node<Token>, sign);
                    Collect(n.Right as Node<Token>, -sign);
                    return;
                }
            }
            if (sign >= 0) numerators.Add(n);
            else denominators.Add(n);
        }

        Collect(root, +1);

        if (numerators.Count + denominators.Count <= 2) return; // nothing to normalize

        // Decide if we should reorder: prioritize when there is at least one numeric literal
        // in the numerator (or a mix numeric/non-numeric in numerator).
        bool hasNumLit = numerators.Any(n => IsNumericLiteral(n, typeMap));
        bool mixNumNon = numerators.Any(n => IsNumeric(n, typeMap)) && numerators.Any(n => !IsNumeric(n, typeMap));
        if (!(hasNumLit || mixNumNon)) return;

        // Rank: numeric literals first, then numeric, then non-numeric. Stable within groups.
        static int Rank(Node<Token> n, Dictionary<Node<Token>, Type?> tm)
            => IsNumericLiteral(n, tm) ? 0
             : IsNumeric(n, tm)        ? 1
             :                           2;

        var orderedNumerators = numerators.OrderBy(n => Rank(n, typeMap)).ToList();

        // Combine back: all numerators first, then denominators (kept in original order)
        var orderedAll = new List<Node<Token>>(orderedNumerators.Count + denominators.Count);
        orderedAll.AddRange(orderedNumerators);
        orderedAll.AddRange(denominators);

        // Collect all '*' and '/' operator nodes across the chain (post-order)
        var opNodes = new List<Node<Token>>();
        void CollectOps(Node<Token>? n)
        {
            if (n is null) return;
            if (n.Value is Token t && t.TokenType == TokenType.Operator && (t.Text == "*" || t.Text == "/"))
            {
                CollectOps(n.Left as Node<Token>);
                CollectOps(n.Right as Node<Token>);
                opNodes.Add(n);
            }
        }
        CollectOps(root);

        if (opNodes.Count != orderedAll.Count - 1) return;

        // Rewire in place. First (numerators.Count - 1) ops become '*', the rest become '/'
        var acc = orderedAll[0];
        for (int i = 0; i < opNodes.Count; i++)
        {
            var opNode = opNodes[i];
            var token = (Token)opNode.Value!;
            var isMultiplyPhase = i < (orderedNumerators.Count - 1);

            token.Text = isMultiplyPhase ? "*" : "/"; // adjust operator kind
            opNode.Left = acc;
            opNode.Right = orderedAll[i + 1];
            acc = opNode;
        }
    }

    private static bool IsNumeric(Node<Token> node, Dictionary<Node<Token>, Type?> typeMap)
    {
        if (!typeMap.TryGetValue(node, out var t) || t is null) return false;
        return IsNumericType(t);
    }

    private static bool IsNumericLiteral(Node<Token> node, Dictionary<Node<Token>, Type?> typeMap)
    {
        if (node.Value is Token t && t.TokenType == TokenType.Literal)
        {
            // Confirm it's a numeric literal via inferred type
            return IsNumeric(node, typeMap);
        }
        return false;
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
        Dictionary<Node<Token>, object?> boxedMap = [];

        foreach (var token in postfixTokens)
        {
            var node = GetNode(token);

            switch (token.TokenType)
            {
                case TokenType.Literal:
                    {
                        var t =  token.IsNull ? null : EvaluateLiteralType(token.Text, token.CaptureGroup);
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
                        var args = node.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator.ToString(), boxedMap);
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