namespace ParserLibrary.ExpressionTree;

public partial class TokenTree
{
    // ── Public entry point ────────────────────────────────────────────────

    /// <summary>
    /// Compresses the expression tree by finding repeated subtrees, replacing each with a
    /// temporary variable (<c>_T1</c>, <c>_T2</c>, …), and returning the ordered evaluation
    /// plan together with the final compressed expression.
    /// </summary>
    /// <param name="patterns">
    /// The <see cref="TokenPatterns"/> used by the parser (needed for expression formatting).
    /// </param>
    /// <param name="tempVarPrefix">Prefix for generated temporary variable names.</param>
    /// <param name="minOccurrences">
    /// Minimum number of occurrences for a subtree to be eligible for extraction (default 2).
    /// </param>
    /// <param name="minDepth">
    /// Minimum subtree depth (height) to be eligible — prevents extracting single-token leaves
    /// (default 1 means at least one operator/function node with children).
    /// </param>
    /// <param name="nextTempVarName">
    /// Optional factory that returns the next unique temp variable name to use, given the
    /// current set of names already present in the plan.  When <c>null</c>, an internal
    /// counter starting at 1 is used (<c>_T1</c>, <c>_T2</c>, …).
    /// Supply a delegate backed by <see cref="ITempVariableNameResolver"/> to guarantee
    /// uniqueness across the whole parser session (e.g. when lambda compressions and the
    /// outer expression share the same variable dictionary).
    /// </param>
    /// <param name="keepOriginalTree">
    /// When <c>true</c>, the method works on a deep clone, leaving this tree intact.
    /// When <c>false</c> (default), the method mutates <b>this</b> tree in place — no clone
    /// is made, which is significantly faster for large trees.
    /// </param>
    /// <returns>A <see cref="CompressionResult"/> with the ordered plan and compressed expression.</returns>
    public CompressionResult Compress(
        TokenPatterns patterns,
        string tempVarPrefix = "_T",
        int minOccurrences = 2,
        int minDepth = 1,
        Func<ICollection<string>, string>? nextTempVarName = null,
        bool keepOriginalTree = false,
        bool compressConstantOnlySubtrees = false)
    {
        // Either clone or work in place depending on the flag.
        var workTree = keepOriginalTree ? DeepCloneTyped() : this;

        var plan = new List<CompressionEntry>();
        int counter = 1; // used only when nextTempVarName is null

        // Maps each temp var name back to its fully-expanded original expression
        // (no temp vars), used to populate OriginalExpression in later plan entries.
        var originalByTemp = new Dictionary<string, string>(StringComparer.Ordinal);

        // Iteratively extract the shallowest (innermost) repeated subtrees first so that
        // the plan is in natural bottom-up evaluation order: inner atoms before outer wrappers.
        // Each iteration performs one post-order analysis and then rewrites all eligible
        // candidates at the shallowest depth in deterministic order to reduce total passes.
        while (true)
        {
            var analysis = AnalyzeTree(workTree.Root, patterns);
            var batchKeys = SelectBatchCandidates(
                analysis.Groups,
                minOccurrences,
                minDepth,
                compressConstantOnlySubtrees,
                patterns);

            if (batchKeys.Count == 0)
                break;

            bool anyReplacement = false;

            foreach (var key in batchKeys)
            {
                var group = analysis.Groups[key];

                // Assign temp variable name — use the external resolver when provided so
                // the caller can guarantee cross-session uniqueness (e.g. lambda vs outer expr).
                string tempVar = nextTempVarName is not null
                    ? nextTempVarName(originalByTemp.Keys)
                    : $"{tempVarPrefix}{counter++}";

                // Record the plan entry.
                // SubstitutedSubtree is normalized so that dependencies on previously
                // introduced temp vars appear as Identifier leaves (e.g. _T1, _T2).
                var substitutedSubtree = NormalizeSubstitutedSubtree(group.Nodes[0].DeepClone(), plan, patterns);
                string substitutedExpr = ExpressionFormatter.Format(substitutedSubtree, patterns);

                // OriginalExpression always shows the fully expanded raw expression.
                string originalExpr = BackExpand(substitutedExpr, originalByTemp);
                originalByTemp[tempVar] = originalExpr;

                plan.Add(new CompressionEntry(
                    TempVariable: tempVar,
                    OriginalExpression: originalExpr,
                    SubstitutedExpression: substitutedExpr,
                    SubstitutedSubtree: substitutedSubtree,
                    OccurrenceCount: group.Nodes.Count
                ));

                // Replace every occurrence in the working tree with a leaf identifier node.
                var tempToken = new Token(TokenType.Identifier, tempVar, int.MaxValue - counter);
                foreach (var target in group.Nodes)
                    ReplaceNodeInPlace(workTree, target, tempToken);

                anyReplacement = true;
            }

            if (!anyReplacement)
                break;

            // Rebuild the dictionary once after the whole batch rewrite.
            workTree.RebuildNodeDictionaryFromStructure();
        }

        string compressedExpr = workTree.GetExpressionString(patterns, spacesAroundOperators: false);

        return new CompressionResult
        {
            Plan = plan,
            CompressedExpression = compressedExpr,
            CompressedTree = workTree
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private sealed class CompressionAnalysis(Dictionary<string, CompressionGroup> groups)
    {
        public Dictionary<string, CompressionGroup> Groups { get; } = groups;
    }

    private sealed class CompressionGroup(int depth, bool isCompressible)
    {
        public int Depth { get; } = depth;
        public bool IsCompressible { get; } = isCompressible;
        public List<Node<Token>> Nodes { get; } = [];
    }

    private readonly record struct NodeAnalysis(
        int Height,
        bool ContainsIdentifierOrFunction);

    /// <summary>
    /// Performs one post-order analysis pass and groups equivalent non-leaf subtrees by
    /// deterministic structural key while caching depth and compressibility metadata.
    /// </summary>
    private static CompressionAnalysis AnalyzeTree(Node<Token> root, TokenPatterns patterns)
    {
        var groups = new Dictionary<string, CompressionGroup>(StringComparer.Ordinal);
        AnalyzeNode(root, groups, patterns);
        return new CompressionAnalysis(groups);
    }

    private static NodeAnalysis AnalyzeNode(
        Node<Token> node,
        Dictionary<string, CompressionGroup> groups,
        TokenPatterns patterns)
    {
        int maxChildHeight = 0;
        bool containsIdentifierOrFunction = false;

        if (node.Left is Node<Token> left)
        {
            var leftInfo = AnalyzeNode(left, groups, patterns);
            maxChildHeight = Math.Max(maxChildHeight, leftInfo.Height);
            containsIdentifierOrFunction |= leftInfo.ContainsIdentifierOrFunction;
        }

        if (node.Right is Node<Token> right)
        {
            var rightInfo = AnalyzeNode(right, groups, patterns);
            maxChildHeight = Math.Max(maxChildHeight, rightInfo.Height);
            containsIdentifierOrFunction |= rightInfo.ContainsIdentifierOrFunction;
        }

        if (node.Other is not null)
        {
            foreach (var child in node.Other.OfType<Node<Token>>())
            {
                var childInfo = AnalyzeNode(child, groups, patterns);
                maxChildHeight = Math.Max(maxChildHeight, childInfo.Height);
                containsIdentifierOrFunction |= childInfo.ContainsIdentifierOrFunction;
            }
        }

        if (node.Value is not null &&
            (node.Value.TokenType == TokenType.Identifier || node.Value.TokenType == TokenType.Function))
            containsIdentifierOrFunction = true;

        int height = maxChildHeight + 1;

        if (node.IsLeaf || node.Value is null || node.Value.IsNull)
            return new NodeAnalysis(height, containsIdentifierOrFunction);

        // ArgumentSeparator nodes are structural glue inside function argument lists;
        // they cannot be evaluated in isolation, so skip grouping them as candidates.
        if (node.Value.TokenType == TokenType.ArgumentSeparator)
            return new NodeAnalysis(height, containsIdentifierOrFunction);

        string key = ExpressionFormatter.Format(node, patterns);
        if (!patterns.CaseSensitive)
            key = key.ToUpperInvariant();

        if (!groups.TryGetValue(key, out var group))
        {
            group = new CompressionGroup(depth: height - 1, isCompressible: containsIdentifierOrFunction);
            groups[key] = group;
        }

        group.Nodes.Add(node);

        return new NodeAnalysis(height, containsIdentifierOrFunction);
    }

    /// <summary>
    /// Selects all extractable candidates at the shallowest eligible depth.
    /// Stable ordering: higher occurrence count first, then structural key ordinal.
    /// </summary>
    private static List<string> SelectBatchCandidates(
        Dictionary<string, CompressionGroup> groups,
        int minOccurrences,
        int minDepth,
        bool compressConstantOnlySubtrees,
        TokenPatterns patterns)
    {
        int bestDepth = int.MaxValue;

        foreach (var kvp in groups)
        {
            var group = kvp.Value;
            if (group.Nodes.Count < minOccurrences) continue;
            if (group.Depth < minDepth) continue;
            if (!compressConstantOnlySubtrees && !group.IsCompressible) continue;

            if (group.Depth < bestDepth)
                bestDepth = group.Depth;
        }

        if (bestDepth == int.MaxValue)
            return [];

        return groups
            .Where(kvp =>
                kvp.Value.Depth == bestDepth
                && kvp.Value.Nodes.Count >= minOccurrences
                && (compressConstantOnlySubtrees || kvp.Value.IsCompressible)
                && !ShouldSkipAssociativeLadder(kvp.Value.Nodes[0], patterns))
            .OrderByDescending(kvp => kvp.Value.Nodes.Count)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private static bool ShouldSkipAssociativeLadder(Node<Token> node, TokenPatterns patterns)
    {
        if (node.Value is null || node.Value.TokenType != TokenType.Operator)
            return false;

        string op = NormalizeCase(node.Value.Text, patterns.CaseSensitive);
        if (!IsAssociativeLadderOperator(op))
            return false;

        var operandExpressions = new List<string>();
        CollectAssociativeOperands(node, op, patterns, operandExpressions);

        if (operandExpressions.Count < 3)
            return false;

        var comparer = patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var freq = new Dictionary<string, int>(comparer);
        foreach (var expr in operandExpressions)
        {
            if (!freq.TryAdd(expr, 1))
                freq[expr]++;
        }

        int distinct = freq.Count;
        int maxCount = freq.Values.Max();

        return distinct <= 2 && maxCount >= operandExpressions.Count - 1;
    }

    private static bool IsAssociativeLadderOperator(string op) =>
        op is "+" or "OR" or "AND" or "&";

    private static void CollectAssociativeOperands(
        Node<Token> node,
        string normalizedOperator,
        TokenPatterns patterns,
        List<string> operands)
    {
        if (node.Value is not null
            && node.Value.TokenType == TokenType.Operator
            && NormalizeCase(node.Value.Text, patterns.CaseSensitive) == normalizedOperator)
        {
            if (node.Left is Node<Token> left)
                CollectAssociativeOperands(left, normalizedOperator, patterns, operands);

            if (node.Right is Node<Token> right)
                CollectAssociativeOperands(right, normalizedOperator, patterns, operands);

            return;
        }

        string expr = ExpressionFormatter.Format(node, patterns);
        operands.Add(NormalizeCase(expr, patterns.CaseSensitive));
    }

    private static string NormalizeCase(string text, bool caseSensitive) =>
        caseSensitive ? text : text.ToUpperInvariant();

    /// <summary>
    /// Replaces <paramref name="target"/> in the working tree with a new leaf node carrying
    /// <paramref name="tempToken"/>.  Uses the pre-built <see cref="Tree{T}.ParentMap"/> when
    /// available (O(1) parent lookup); falls back to a DFS walk otherwise.
    /// </summary>
    private static void ReplaceNodeInPlace(TokenTree tree, Node<Token> target, Token tempToken)
    {
        if (target == tree.Root)
        {
            // Replacing the root itself.
            var newRoot = new Node<Token>(tempToken);
            tree.Root = newRoot;
            return;
        }

        // Fast path: use the parent map built at compile time (or by BuildParentMap()).
        if (tree.ParentMap is { } pm && pm.TryGetValue(target, out var parent) && parent is not null)
        {
            ReplaceChildSlot(parent, target, tempToken);
            return;
        }

        // Fallback: walk the tree to find the parent of target.
        ReplaceChild(tree.Root, target, tempToken);
    }

    /// <summary>
    /// Replaces <paramref name="target"/> in one of <paramref name="parent"/>'s child slots
    /// with a new leaf node. Does not recurse — the slot must be a direct child of
    /// <paramref name="parent"/>.
    /// </summary>
    private static void ReplaceChildSlot(Node<Token> parent, Node<Token> target, Token tempToken)
    {
        if (parent.Left == target) { parent.Left = new Node<Token>(tempToken); return; }
        if (parent.Right == target) { parent.Right = new Node<Token>(tempToken); return; }
        if (parent.Other is not null)
        {
            for (int i = 0; i < parent.Other.Count; i++)
            {
                if (parent.Other[i] == target)
                {
                    parent.Other[i] = new Node<Token>(tempToken);
                    return;
                }
            }
        }
    }

    private static bool ReplaceChild(Node<Token> current, Node<Token> target, Token tempToken)
    {
        // Check Left slot.
        if (current.Left is Node<Token> l)
        {
            if (l == target)
            {
                current.Left = new Node<Token>(tempToken);
                return true;
            }
            if (ReplaceChild(l, target, tempToken)) return true;
        }

        // Check Right slot.
        if (current.Right is Node<Token> r)
        {
            if (r == target)
            {
                current.Right = new Node<Token>(tempToken);
                return true;
            }
            if (ReplaceChild(r, target, tempToken)) return true;
        }

        // Check Other slots.
        if (current.Other is not null)
        {
            for (int i = 0; i < current.Other.Count; i++)
            {
                if (current.Other[i] is Node<Token> o)
                {
                    if (o == target)
                    {
                        current.Other[i] = new Node<Token>(tempToken);
                        return true;
                    }
                    if (ReplaceChild(o, target, tempToken)) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Replaces inside <paramref name="subtree"/> each previously-extracted expression
    /// with the corresponding temp-variable Identifier leaf.
    /// </summary>
    private static Node<Token> NormalizeSubstitutedSubtree(
        Node<Token> subtree,
        IReadOnlyList<CompressionEntry> plan,
        TokenPatterns patterns)
    {
        if (plan.Count == 0) return subtree;

        var byExpr = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in plan)
            byExpr[entry.SubstitutedExpression] = entry.TempVariable;

        int nextTempIndex = int.MaxValue;

        Node<Token> Rewrite(Node<Token> node)
        {
            if (node.IsLeaf || node.Value is null || node.Value.IsNull)
                return node;

            if (node.Left is Node<Token> l)
                node.Left = Rewrite(l);
            if (node.Right is Node<Token> r)
                node.Right = Rewrite(r);
            if (node.Other is not null)
            {
                for (int i = 0; i < node.Other.Count; i++)
                {
                    if (node.Other[i] is Node<Token> o)
                        node.Other[i] = Rewrite(o);
                }
            }

            string expr = ExpressionFormatter.Format(node, patterns);
            if (byExpr.TryGetValue(expr, out var tempVar))
            {
                return new Node<Token>(new Token(TokenType.Identifier, tempVar, nextTempIndex--));
            }

            return node;
        }

        return Rewrite(subtree);
    }

    /// <summary>
    /// Replaces every temp-variable reference inside <paramref name="expr"/> with the
    /// fully-expanded original expression stored in <paramref name="originalByTemp"/>,
    /// recursively, so the result contains only the raw source variables.
    /// </summary>
    private static string BackExpand(string expr, Dictionary<string, string> originalByTemp)
    {
        // Replace longest temp names first to avoid partial matches (e.g. _T10 before _T1).
        foreach (var kvp in originalByTemp.OrderByDescending(k => k.Key.Length))
            expr = expr.Replace(kvp.Key, kvp.Value);
        return expr;
    }
}
