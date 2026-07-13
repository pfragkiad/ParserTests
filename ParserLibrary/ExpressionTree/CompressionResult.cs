using System.Text;

namespace ParserLibrary.ExpressionTree;

/// <summary>
/// Result produced by <see cref="TokenTree.Compress"/>.
/// </summary>
public sealed class CompressionResult
{
    /// <summary>
    /// Ordered list of subexpression assignments that must be evaluated in order.
    /// </summary>
    public IReadOnlyList<CompressionEntry> Entries { get; init; } = [];

    /// <summary>
    /// Case-sensitivity used while producing this compression result.
    /// Must stay aligned with <see cref="TokenPatterns.CaseSensitive"/>.
    /// </summary>
    public bool CaseSensitive { get; init; }

    public bool IsCompressed { get; init; }

    /// <summary>
    /// The final compressed expression string using temp variable names.
    /// </summary>
    public string CompressedExpression { get; init; } = string.Empty; //SubstitutedExpression

    /// <summary>
    /// The compressed expression tree (cloned, original is untouched).
    /// Temp variable leaves appear as <see cref="TokenType.Identifier"/> nodes.
    /// </summary>
    public TokenTree? CompressedTree { get; init; }

    /// <summary>
    /// Number of distinct repeated subexpressions that were extracted.
    /// Returns 0 when no compression happened, even though a single passthrough
    /// entry may exist to support downstream evaluation APIs.
    /// </summary>
    public int SubstitutionCount => IsCompressed ? Entries.Count : 0;

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
        StringComparer dependencyComparer = CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        foreach (var entry in Entries)
        {
            string expr = withCalculation ? entry.SubstitutedExpression : entry.OriginalExpression;

            sb.Append($"{entry.TempVariable} = {expr}   // occurrences: {entry.OccurrenceCount}");

            if (showDirectDependencies)
            {
                sb.Append($" | direct deps: {FormatDependencies(entry.Dependencies, dependencyComparer)}");
            }

            if (showAllDependencies)
            {
                IReadOnlyList<string> allDependencies = TokenTree.CollectDependencyChainOrdered(Entries, entry.TempVariable, CaseSensitive);
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
