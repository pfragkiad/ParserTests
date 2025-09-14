namespace ParserLibrary.ExpressionTree;

public readonly struct TreeOptimizerResult
{
    public required TokenTree Tree { get; init; }

    // Metrics are optional; when not computed, they are null.
    public int? NonAllNumericBefore { get; init; }
    public int? NonAllNumericAfter { get; init; }

    public bool HasMetrics => NonAllNumericBefore.HasValue && NonAllNumericAfter.HasValue;

    public int? Improvement =>
        HasMetrics ? NonAllNumericBefore!.Value - NonAllNumericAfter!.Value : null;

    public bool IsImproved => Improvement.HasValue && Improvement.Value > 0;

    // Factory for a no-op optimization result (no metrics computed)
    public static TreeOptimizerResult Unchanged(TokenTree tree) => new()
    {
        Tree = tree,
        NonAllNumericBefore = null,
        NonAllNumericAfter = null
    };

    public override string ToString() =>
        HasMetrics
            ? $"TreeOptimizerResult(NonAllNumericBefore={NonAllNumericBefore}, NonAllNumericAfter={NonAllNumericAfter}, Improvement={Improvement})"
            : "TreeOptimizerResult(unchanged)";
}