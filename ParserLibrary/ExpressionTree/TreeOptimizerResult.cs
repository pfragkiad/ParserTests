namespace ParserLibrary.ExpressionTree;

public readonly struct TreeOptimizerResult
{
    public required TokenTree Tree { get; init; }
    public required int NonAllNumericBefore { get; init; }
    public required int NonAllNumericAfter { get; init; }
    public int Improvement => NonAllNumericBefore - NonAllNumericAfter;
    public override string ToString() =>
        $"TreeOptimizerResult(NonAllNumericBefore={NonAllNumericBefore}, NonAllNumericAfter={NonAllNumericAfter}, Improvement={Improvement})";
}