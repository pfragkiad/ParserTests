using System.Text;

namespace ParserLibrary.ExpressionTree;

/// <summary>
/// A single entry in the compression plan: a temporary variable and the expression it represents.
/// </summary>
public sealed record CompressionEntry(
    /// <summary>The temporary variable name, e.g. "_T1".</summary>
    string TempVariable,
    /// <summary>The original (unsubstituted) expression string for this subexpression.</summary>
    string OriginalExpression,
    /// <summary>
    /// The substituted expression string — same as OriginalExpression for first-level entries,
    /// but uses previously defined temp variables for deeper levels.
    /// </summary>
    string SubstitutedExpression,
    /// <summary>
    /// A deep-cloned subtree for this expression in substituted form, ready for fast evaluation.
    /// </summary>
    Node<Token> SubstitutedSubtree,
    /// <summary>How many times this subexpression appeared in the tree.</summary>
    int OccurrenceCount,
    /// <summary>
    /// Temp variable names referenced by <see cref="SubstitutedSubtree"/>.
    /// Comparer respects <see cref="TokenPatterns.CaseSensitive"/> from compression.
    /// </summary>
    HashSet<string> Dependencies
);

/// <summary>
/// Result produced by <see cref="TokenTree.Compress"/>.
/// </summary>
public sealed class CompressionResult
{
    /// <summary>
    /// Ordered list of subexpression assignments that must be evaluated in order.
    /// </summary>
    public IReadOnlyList<CompressionEntry> Entries { get; init; } = [];

    public bool IsCompressed => Entries.Count > 0;

    /// <summary>
    /// The final compressed expression string using temp variable names.
    /// </summary>
    public string CompressedExpression { get; init; } = string.Empty;

    /// <summary>
    /// The compressed expression tree (cloned, original is untouched).
    /// Temp variable leaves appear as <see cref="TokenType.Identifier"/> nodes.
    /// </summary>
    public TokenTree? CompressedTree { get; init; }

    /// <summary>
    /// Number of distinct repeated subexpressions that were extracted.
    /// </summary>
    public int SubstitutionCount => Entries.Count;

    // ── Display helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns a human-readable plan string.
    /// </summary>
    /// <param name="withCalculation">
    /// When <c>true</c>, shows each step as "<c>_T1 = &lt;substituted&gt;</c>" (ready-to-evaluate form).
    /// When <c>false</c>, shows "<c>_T1 = &lt;original&gt;</c>" (unsubstituted, easier to read).
    /// </param>
    /// <param name="showDirectDependencies">
    /// When <c>true</c>, appends direct temp-variable dependencies for each compression entry.
    /// </param>
    /// <param name="showAllDependencies">
    /// When <c>true</c>, appends all dependencies (direct + transitive) for each compression entry.
    /// </param>
    public string GetPlanText(
        bool withCalculation = true,
        bool showDirectDependencies = false,
        bool showAllDependencies = false)
    {
        var sb = new StringBuilder();
        foreach (var entry in Entries)
        {
            string expr = withCalculation ? entry.SubstitutedExpression : entry.OriginalExpression;
            sb.Append($"{entry.TempVariable} = {expr}   // occurrences: {entry.OccurrenceCount}");

            StringComparer dependencyComparer = GetDependencyComparer(entry.Dependencies.Comparer);

            if (showDirectDependencies)
            {
                sb.Append($" | direct deps: {FormatDependencies(entry.Dependencies, dependencyComparer)}");
            }

            if (showAllDependencies)
            {
                IReadOnlyList<string> allDependencies = TokenTree.CollectDependencyChainOrdered(Entries, entry.TempVariable, dependencyComparer == StringComparer.Ordinal);
                sb.Append($" | all deps: {FormatDependenciesInExistingOrder(allDependencies, dependencyComparer)}");
            }

            sb.AppendLine();
        }
        sb.AppendLine();
        sb.Append($"result = {CompressedExpression}");
        return sb.ToString();
    }

    private static string FormatDependencies(IEnumerable<string> dependencies, StringComparer comparer)
    {
        string[] orderedDependencies = [.. dependencies.Distinct(comparer).OrderBy(d => d, comparer)];
        return orderedDependencies.Length == 0 ? "(none)" : string.Join(", ", orderedDependencies);
    }

    private static string FormatDependenciesInExistingOrder(IEnumerable<string> dependencies, StringComparer comparer)
    {
        string[] orderedDependencies = [.. dependencies.Distinct(comparer)];
        return orderedDependencies.Length == 0 ? "(none)" : string.Join(", ", orderedDependencies);
    }

    private static StringComparer GetDependencyComparer(IEqualityComparer<string>? comparer)
    {
        return comparer == StringComparer.Ordinal
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
    }

    /// <inheritdoc cref="GetPlanText(bool, bool, bool)"/>
    public void PrintPlan(
        bool withCalculation = true,
        bool showDirectDependencies = false,
        bool showAllDependencies = false) =>
        Console.WriteLine(GetPlanText(withCalculation, showDirectDependencies, showAllDependencies));

    /// <summary>
    /// Prints a side-by-side section showing the original tree, the compressed tree,
    /// and then the plan (both views).
    /// </summary>
    /// <param name="originalTree">The uncompressed tree (before calling Compress).</param>
    /// <param name="label">Optional label used in the section header.</param>
    public void PrintFull(TokenTree originalTree, string label = "")
    {
        string header = string.IsNullOrWhiteSpace(label) ? "Compression" : label;

        Console.WriteLine($"  Original  ({originalTree.Count} nodes, height {originalTree.GetHeight()}):");
        originalTree.Print(PrintType.Vertical);

        if (CompressedTree is not null)
        {
            Console.WriteLine($"  Compressed ({CompressedTree.Count} nodes, height {CompressedTree.GetHeight()}):");
            CompressedTree.Print(PrintType.Vertical);
        }

        Console.WriteLine("--- Plan (original, without substitution) ---");
        Console.WriteLine(GetPlanText(withCalculation: false));
        Console.WriteLine("--- Plan (substituted, evaluation order) ---");
        Console.WriteLine(GetPlanText(withCalculation: true));
    }

    public override string ToString() => GetPlanText(withCalculation: true);
}
