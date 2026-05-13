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
    /// <summary>How many times this subexpression appeared in the tree.</summary>
    int OccurrenceCount
);

/// <summary>
/// Result produced by <see cref="TokenTree.Compress"/>.
/// </summary>
public sealed class CompressionResult
{
    /// <summary>
    /// Ordered list of subexpression assignments that must be evaluated in order.
    /// </summary>
    public IReadOnlyList<CompressionEntry> Plan { get; init; } = [];

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
    public int SubstitutionCount => Plan.Count;

    // ── Display helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns a human-readable plan string.
    /// </summary>
    /// <param name="withCalculation">
    /// When <c>true</c>, shows each step as "<c>_T1 = &lt;substituted&gt;</c>" (ready-to-evaluate form).
    /// When <c>false</c>, shows "<c>_T1 = &lt;original&gt;</c>" (unsubstituted, easier to read).
    /// </param>
    public string GetPlanText(bool withCalculation = true)
    {
        var sb = new StringBuilder();
        foreach (var entry in Plan)
        {
            string expr = withCalculation ? entry.SubstitutedExpression : entry.OriginalExpression;
            sb.AppendLine($"{entry.TempVariable} = {expr}   // occurrences: {entry.OccurrenceCount}");
        }
        sb.AppendLine();
        sb.Append($"result = {CompressedExpression}");
        return sb.ToString();
    }

    /// <inheritdoc cref="GetPlanText(bool)"/>
    public void PrintPlan(bool withCalculation = true) =>
        Console.WriteLine(GetPlanText(withCalculation));

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
