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
    /// When <c>false</c> (default) the method works on a deep clone, leaving this tree intact.
    /// When <c>true</c> the method mutates <b>this</b> tree in place — no clone is made, which
    /// is significantly faster for large trees.  Use this when you no longer need the original
    /// tree after compression (e.g. you parsed it solely to compress it).
    /// </param>
    /// <returns>A <see cref="CompressionResult"/> with the ordered plan and compressed expression.</returns>
    public CompressionResult Compress(
        TokenPatterns patterns,
        string tempVarPrefix = "_T",
        int minOccurrences = 2,
        int minDepth = 1,
        Func<ICollection<string>, string>? nextTempVarName = null,
        bool keepOriginalTree = true,
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
        // the plan is in natural bottom-up evaluation order:  inner atoms before outer wrappers.
        // This ensures that SubstitutedExpression (uses temp vars) and OriginalExpression
        // (back-expanded to raw variables) are meaningfully different for wrapped entries.
        while (true)
        {
            // 1. Compute canonical expression string for every non-leaf node (bottom-up).
            var exprMap = new Dictionary<Node<Token>, string>();
            BuildExpressionMap(workTree.Root, exprMap, patterns);

            // 2. Count occurrences of each expression string across the tree.
            var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var expr in exprMap.Values)
                frequency[expr] = frequency.TryGetValue(expr, out int c) ? c + 1 : 1;

            // 3. Find candidates: repeated non-leaf subtrees with enough occurrences and depth.
            //    Pick the SHALLOWEST (smallest height) candidate first — innermost atoms before
            //    their wrappers — so the plan evaluates bottom-up.
            //    Tie-break: prefer higher occurrence count, then alphabetical for determinism.
            string? best = null;
            int bestDepth = int.MaxValue;
            int bestCount = 0;

            foreach (var kvp in frequency)
            {
                if (kvp.Value < minOccurrences) continue;

                var rep = exprMap.FirstOrDefault(e => e.Value == kvp.Key).Key;
                if (rep is null) continue;
                int depth = rep.GetHeight() - 1; // height-1 ~ edge depth
                if (depth < minDepth) continue;

                // Skip pure constant-arithmetic subtrees (no identifier and no function
                // call) when the caller has not opted in to compressing them.
                // e.g. -1, 1/3 are skipped, but halfhourly(0) or -_T1 are kept.
                if (!compressConstantOnlySubtrees && !SubtreeIsCompressible(rep)) continue;

                bool better = depth < bestDepth
                    || (depth == bestDepth && kvp.Value > bestCount)
                    || (depth == bestDepth && kvp.Value == bestCount
                        && string.Compare(kvp.Key, best, StringComparison.Ordinal) < 0);

                if (better)
                {
                    bestDepth = depth;
                    bestCount = kvp.Value;
                    best = kvp.Key;
                }
            }

            if (best is null) break; // nothing more to extract

            // 4. Assign temp variable name — use the external resolver when provided so
            //    the caller can guarantee cross-session uniqueness (e.g. lambda vs outer expr).
            string tempVar = nextTempVarName is not null
                ? nextTempVarName(originalByTemp.Keys)
                : $"{tempVarPrefix}{counter++}";

            // 5. Collect all nodes that carry this expression (could be >1 occurrence).
            var targets = exprMap
                .Where(e => e.Value == best)
                .Select(e => e.Key)
                .ToList();

            // 6. Record the plan entry.
            //    SubstitutedExpression = 'best' as-is (contains any earlier temp vars).
            //    OriginalExpression    = 'best' with all temp var references expanded back
            //                           to their original raw-variable forms, so the reader
            //                           can audit each step without chasing temp var definitions.
            string originalExpr = BackExpand(best, originalByTemp);
            originalByTemp[tempVar] = originalExpr;

            plan.Add(new CompressionEntry(
                TempVariable: tempVar,
                OriginalExpression: originalExpr,
                SubstitutedExpression: best,
                OccurrenceCount: targets.Count
            ));

            // 7. Replace every occurrence in the working tree with a leaf identifier node.
            var tempToken = new Token(TokenType.Identifier, tempVar, int.MaxValue - counter);

            foreach (var target in targets)
                ReplaceNodeInPlace(workTree, target, tempToken);

            // Rebuild the dictionary after structural changes.
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

    /// <summary>
    /// Populates <paramref name="exprMap"/> (node → expression string) for every non-leaf
    /// node in the subtree rooted at <paramref name="node"/>, bottom-up.
    /// </summary>
    private static void BuildExpressionMap(
        Node<Token> node,
        Dictionary<Node<Token>, string> exprMap,
        TokenPatterns patterns)
    {
        // Recurse into children first (post-order).
        if (node.Left is Node<Token> left)
            BuildExpressionMap(left, exprMap, patterns);

        if (node.Right is Node<Token> right)
            BuildExpressionMap(right, exprMap, patterns);

        if (node.Other is not null)
            foreach (var child in node.Other.OfType<Node<Token>>())
                BuildExpressionMap(child, exprMap, patterns);

        // Only map non-leaf nodes.
        if (node.IsLeaf) return;
        if (node.Value is null || node.Value.IsNull) return;

        // ArgumentSeparator nodes are structural glue inside function argument lists;
        // they cannot be evaluated in isolation, so skip them.
        if (node.Value.TokenType == TokenType.ArgumentSeparator) return;

        string expr = ExpressionFormatter.Format(node, patterns);
        exprMap[node] = expr;
    }

    /// <summary>
    /// Replaces <paramref name="target"/> in the working tree with a new leaf node carrying
    /// <paramref name="tempToken"/>.  Handles Left, Right and Other child slots.
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

        // Walk the tree to find the parent of target.
        ReplaceChild(tree.Root, target, tempToken);
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
    /// Returns <c>true</c> when the subtree rooted at <paramref name="node"/> is worth
    /// compressing even when <c>compressConstantOnlySubtrees</c> is <c>false</c>.
    /// A subtree qualifies when it contains at least one <see cref="TokenType.Identifier"/>
    /// (variable reference) or at least one <see cref="TokenType.Function"/> node (function
    /// call). Pure constant/literal arithmetic — e.g. <c>-1</c>, <c>1/3</c> — returns
    /// <c>false</c>, while <c>halfhourly(0)</c> or <c>-_T1</c> return <c>true</c>.
    /// </summary>
    private static bool SubtreeIsCompressible(Node<Token> node)
    {
        if (node.Value is not null &&
            (node.Value.TokenType == TokenType.Identifier ||
             node.Value.TokenType == TokenType.Function))
            return true;

        if (node.Left is Node<Token> l && SubtreeIsCompressible(l)) return true;
        if (node.Right is Node<Token> r && SubtreeIsCompressible(r)) return true;

        if (node.Other is not null)
            foreach (var child in node.Other.OfType<Node<Token>>())
                if (SubtreeIsCompressible(child)) return true;

        return false;
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
