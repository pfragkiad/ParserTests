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
    /// <param name="forceFunctionNames">
    /// Optional function names that must always be compressed when encountered, even when they
    /// appear once. Matching respects <see cref="TokenPatterns.CaseSensitive"/>.
    /// </param>
    /// <param name="forceOperatorSymbols">
    /// Optional operator symbols/text that must always be compressed when encountered, even when
    /// they appear once. Matching respects <see cref="TokenPatterns.CaseSensitive"/>.
    /// </param>
    /// <returns>A <see cref="CompressionResult"/> with the ordered plan and compressed expression.</returns>
    public CompressionResult Compress(
        TokenPatterns patterns,
        string tempVarPrefix = "_T",
        int minOccurrences = 2,
        int minDepth = 1,
        Func<ICollection<string>, string>? nextTempVarName = null,
        bool keepOriginalTree = false,
        bool compressConstantOnlySubtrees = false,
        IReadOnlyList<CompressionEntry>? existingEntries = null,
        HashSet<string>? forcedFunctions = null,
        HashSet<string>? forcedOperators = null)
    {
        // Either clone or work in place depending on the flag.
        var workTree = keepOriginalTree ? DeepCloneTyped() : this;

        //force case-sensitive settings defined by TokenPatterns
        if (forcedFunctions is null)
            forcedFunctions = [];
        else
            forcedFunctions = new HashSet<string>(forcedFunctions, patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        if (forcedOperators is null)
            forcedOperators = [];
        else
            forcedOperators = new HashSet<string>(forcedOperators, patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        List<CompressionEntry> plan = existingEntries is null ? [] : [.. existingEntries];
        int counter = 1; // used only when nextTempVarName is null

        // Maps each temp var name back to its fully-expanded original expression
        // (no temp vars), used to populate OriginalExpression in later plan entries.
        var originalByTemp = new Dictionary<string, string>(StringComparer.Ordinal);

        // Canonical structural key -> already assigned temp variable.
        var tempByStructuralKey = new Dictionary<string, string>(StringComparer.Ordinal);

        var occurrenceByTemp = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < plan.Count; i++)
        {
            var entry = plan[i];
            originalByTemp[entry.TempVariable] = entry.OriginalExpression;

            string seedKey = entry.SubstitutedSubtree is not null
                ? ComputeStructuralKey(entry.SubstitutedSubtree, patterns.CaseSensitive)
                : NormalizeExpressionKey(entry.SubstitutedExpression, patterns);

            tempByStructuralKey[seedKey] = entry.TempVariable;
            occurrenceByTemp[entry.TempVariable] = entry.OccurrenceCount;
        }

        void IncrementOccurrence(string tempVar, int delta)
        {
            if (delta <= 0) return;
            if (!occurrenceByTemp.TryAdd(tempVar, delta))
                occurrenceByTemp[tempVar] += delta;
        }

        if (nextTempVarName is null)
        {
            foreach (var entry in plan)
            {
                if (!entry.TempVariable.StartsWith(tempVarPrefix, StringComparison.Ordinal))
                    continue;

                string suffix = entry.TempVariable[tempVarPrefix.Length..];
                if (int.TryParse(suffix, out int existingIndex) && existingIndex >= counter)
                    counter = existingIndex + 1;
            }
        }

        if (workTree.ParentMap is null)
            workTree.BuildParentMap();

        var effectiveForcedFunctions = forcedFunctions ?? [];
        var effectiveForcedOperators = forcedOperators ?? [];
        bool structureMutated = false;

        // Iteratively extract the shallowest (innermost) repeated subtrees first so that
        // the plan is in natural bottom-up evaluation order: inner atoms before outer wrappers.
        // Each iteration performs one post-order analysis and then rewrites all eligible
        // candidates at the shallowest depth in deterministic order to reduce total passes.
        while (true)
        {
            var analysis = AnalyzeTree(workTree.Root, patterns);

            bool reusedExisting = false;
            foreach (var kvp in analysis.Groups.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (!tempByStructuralKey.TryGetValue(kvp.Key, out string? existingTemp))
                    continue;

                var tempToken = new Token(TokenType.Identifier, existingTemp, int.MaxValue - counter);
                foreach (var target in kvp.Value.Nodes)
                    ReplaceNodeInPlace(workTree, target, tempToken);

                IncrementOccurrence(existingTemp, kvp.Value.Nodes.Count);
                reusedExisting = true;
                structureMutated = true;
            }

            if (reusedExisting)
                continue;

            var batchKeys = SelectBatchCandidates(
                analysis.Groups,
                minOccurrences,
                minDepth,
                compressConstantOnlySubtrees,
                patterns,
                effectiveForcedFunctions,
                effectiveForcedOperators);

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
                tempByStructuralKey[ComputeStructuralKey(substitutedSubtree, patterns.CaseSensitive)] = tempVar;
                occurrenceByTemp[tempVar] = group.Nodes.Count;

                // Replace every occurrence in the working tree with a leaf identifier node.
                var tempToken = new Token(TokenType.Identifier, tempVar, int.MaxValue - counter);
                foreach (var target in group.Nodes)
                    ReplaceNodeInPlace(workTree, target, tempToken);

                anyReplacement = true;
                structureMutated = true;
            }

            if (!anyReplacement)
                break;
        }

        if (structureMutated)
            workTree.RebuildNodeDictionaryFromStructure();

        string compressedExpr = workTree.GetExpressionString(patterns, spacesAroundOperators: false);

        var projectedPlan = new List<CompressionEntry>(plan.Count);
        for (int i = 0; i < plan.Count; i++)
        {
            var entry = plan[i];
            int totalOccurrences = occurrenceByTemp.TryGetValue(entry.TempVariable, out int count)
                ? count
                : entry.OccurrenceCount;

            projectedPlan.Add(new CompressionEntry(
                TempVariable: entry.TempVariable,
                OriginalExpression: entry.OriginalExpression,
                SubstitutedExpression: entry.SubstitutedExpression,
                SubstitutedSubtree: entry.SubstitutedSubtree,
                OccurrenceCount: totalOccurrences));
        }

        return new CompressionResult
        {
            Entries = projectedPlan,
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
        bool ContainsIdentifierOrFunction,
        string StructuralKey);

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

        string leftKey = "\0";
        string rightKey = "\0";
        List<string>? otherKeys = null;

        if (node.Left is Node<Token> left)
        {
            var leftInfo = AnalyzeNode(left, groups, patterns);
            maxChildHeight = Math.Max(maxChildHeight, leftInfo.Height);
            containsIdentifierOrFunction |= leftInfo.ContainsIdentifierOrFunction;
            leftKey = leftInfo.StructuralKey;
        }

        if (node.Right is Node<Token> right)
        {
            var rightInfo = AnalyzeNode(right, groups, patterns);
            maxChildHeight = Math.Max(maxChildHeight, rightInfo.Height);
            containsIdentifierOrFunction |= rightInfo.ContainsIdentifierOrFunction;
            rightKey = rightInfo.StructuralKey;
        }

        if (node.Other is not null)
        {
            for (int i = 0; i < node.Other.Count; i++)
            {
                if (node.Other[i] is not Node<Token> child)
                    continue;

                var childInfo = AnalyzeNode(child, groups, patterns);
                maxChildHeight = Math.Max(maxChildHeight, childInfo.Height);
                containsIdentifierOrFunction |= childInfo.ContainsIdentifierOrFunction;

                otherKeys ??= [];
                otherKeys.Add(childInfo.StructuralKey);
            }
        }

        var nodeValue = node.Value;
        if (nodeValue is not null &&
            (nodeValue.TokenType == TokenType.Identifier || nodeValue.TokenType == TokenType.Function))
            containsIdentifierOrFunction = true;

        int height = maxChildHeight + 1;

        string structuralKey = BuildStructuralKey(nodeValue, leftKey, rightKey, otherKeys, patterns.CaseSensitive);

        if (node.IsLeaf || nodeValue is null || nodeValue.IsNull)
            return new NodeAnalysis(height, containsIdentifierOrFunction, structuralKey);

        // ArgumentSeparator nodes are structural glue inside function argument lists;
        // they cannot be evaluated in isolation, so skip grouping them as candidates.
        if (nodeValue.TokenType == TokenType.ArgumentSeparator)
            return new NodeAnalysis(height, containsIdentifierOrFunction, structuralKey);

        if (!groups.TryGetValue(structuralKey, out var group))
        {
            group = new CompressionGroup(depth: height - 1, isCompressible: containsIdentifierOrFunction);
            groups[structuralKey] = group;
        }

        group.Nodes.Add(node);

        return new NodeAnalysis(height, containsIdentifierOrFunction, structuralKey);
    }

    private static string BuildStructuralKey(
        Token? token,
        string leftKey,
        string rightKey,
        List<string>? otherKeys,
        bool caseSensitive)
    {
        string otherPart = otherKeys is null || otherKeys.Count == 0
            ? "\0"
            : string.Join('\u001F', otherKeys);

        if (token is null)
            return $"N|L:{leftKey}|R:{rightKey}|O:{otherPart}";

        string normalizedText = NormalizeCase(token.Text, caseSensitive);
        return $"{(int)token.TokenType}:{normalizedText}|L:{leftKey}|R:{rightKey}|O:{otherPart}";
    }

    private static string ComputeStructuralKey(Node<Token> node, bool caseSensitive)
    {
        string leftKey = node.Left is Node<Token> left
            ? ComputeStructuralKey(left, caseSensitive)
            : "\0";

        string rightKey = node.Right is Node<Token> right
            ? ComputeStructuralKey(right, caseSensitive)
            : "\0";

        List<string>? otherKeys = null;
        if (node.Other is not null)
        {
            for (int i = 0; i < node.Other.Count; i++)
            {
                if (node.Other[i] is not Node<Token> other)
                    continue;

                otherKeys ??= [];
                otherKeys.Add(ComputeStructuralKey(other, caseSensitive));
            }
        }

        return BuildStructuralKey(node.Value, leftKey, rightKey, otherKeys, caseSensitive);
    }

    private static string NormalizeExpressionKey(string expression, TokenPatterns patterns) =>
        patterns.CaseSensitive ? expression : expression.ToUpperInvariant();

    /// <summary>
    /// Selects all extractable candidates at the shallowest eligible depth.
    /// Stable ordering: higher occurrence count first, then structural key ordinal.
    /// </summary>
    private static List<string> SelectBatchCandidates(
        Dictionary<string, CompressionGroup> groups,
        int minOccurrences,
        int minDepth,
        bool compressConstantOnlySubtrees,
        TokenPatterns patterns,
        HashSet<string> forcedFunctionNames,
        HashSet<string> forcedOperatorSymbols)
    {
        int bestDepth = int.MaxValue;
        var eligible = new List<(string Key, CompressionGroup Group, bool IsForced)>();

        foreach (var kvp in groups)
        {
            var group = kvp.Value;
            bool isForced = IsForcedCandidate(group.Nodes[0], forcedFunctionNames, forcedOperatorSymbols, patterns);

            if (!isForced)
            {
                if (group.Nodes.Count < minOccurrences) continue;
                if (group.Depth < minDepth) continue;
                if (!compressConstantOnlySubtrees && !group.IsCompressible) continue;
                if (ShouldSkipAssociativeLadder(group.Nodes[0], patterns)) continue;
            }

            if (group.Depth < bestDepth)
                bestDepth = group.Depth;

            eligible.Add((kvp.Key, group, isForced));
        }

        if (bestDepth == int.MaxValue)
            return [];

        return eligible
            .Where(x => x.Group.Depth == bestDepth)
            .OrderByDescending(x => x.Group.Nodes.Count)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key)
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

    private static bool IsForcedCandidate(
        Node<Token> node,
        HashSet<string> forcedFunctionNames,
        HashSet<string> forcedOperatorSymbols,
        TokenPatterns patterns)
    {
        if (node.Value is null)
            return false;

        var comparer = patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        if (node.Value.TokenType == TokenType.Function && forcedFunctionNames is not null)
            return forcedFunctionNames.Contains(node.Value.Text, comparer);

        if (node.Value.TokenType == TokenType.Operator && forcedOperatorSymbols is not null)
            return forcedOperatorSymbols.Contains(node.Value.Text, comparer);

        return false;
    }

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
            if (ReplaceChildSlot(parent, target, tempToken))
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
    private static bool ReplaceChildSlot(Node<Token> parent, Node<Token> target, Token tempToken)
    {
        if (parent.Left == target) { parent.Left = new Node<Token>(tempToken); return true; }
        if (parent.Right == target) { parent.Right = new Node<Token>(tempToken); return true; }
        if (parent.Other is not null)
        {
            for (int i = 0; i < parent.Other.Count; i++)
            {
                if (parent.Other[i] == target)
                {
                    parent.Other[i] = new Node<Token>(tempToken);
                    return true;
                }
            }
        }

        return false;
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
