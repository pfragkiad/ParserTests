namespace ParserLibrary.ExpressionTree;

/// <summary>
/// A single entry in the compression plan: a temporary variable and the expression it represents.
/// </summary>
public sealed class CompressionEntry
{
    /// <summary>The temporary variable name, e.g. "_T1".</summary>
    public required string TempVariable { get; init; }

    /// <summary>The original (unsubstituted) expression string for this subexpression.</summary>
    public required string OriginalExpression { get; init; }

    /// <summary>
    /// The substituted expression string — same as OriginalExpression for first-level entries,
    /// but uses previously defined temp variables for deeper levels.
    /// </summary>
    public required string SubstitutedExpression { get; init; }

    public override string ToString() => $"{TempVariable} = {SubstitutedExpression}";

    /// <summary>
    /// A deep-cloned subtree for this expression in substituted form, ready for fast evaluation.
    /// </summary>
    public required Node<Token> SubstitutedSubtree { get; init; }

    /// <summary>How many times this subexpression appeared in the tree.</summary>
    public required int OccurrenceCount { get; init; }

    /// <summary>
    /// Temp variable names referenced by <see cref="SubstitutedSubtree"/>.
    /// Comparer respects <see cref="TokenPatterns.CaseSensitive"/> from compression.
    /// </summary>
    public required HashSet<string> Dependencies { get; init; }


    public bool IsCalculated { get; private set; } = false;


    private object? _result;
    public object? Result
    {
        get => _result;
        set
        {
            //not that null is an accepted value and IsCalculated can be true when result is set to null
            _result = value;
            IsCalculated = true;
        }
    }

    public void ResetResult()
    {
        Result = null;
        IsCalculated = false;
    }



}
