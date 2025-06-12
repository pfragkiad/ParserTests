namespace ParserLibrary.Tokenizers;

public readonly struct ParenthesisCheckResult
{
    public List<int> UnmatchedClosed { get; init; }

    public List<int> UnmatchedOpen { get; init; }

    public bool AreMatched => UnmatchedClosed.Count == 0 && UnmatchedOpen.Count == 0;
} 