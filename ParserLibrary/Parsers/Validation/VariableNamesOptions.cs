namespace ParserLibrary.Parsers.Validation;

public sealed class VariableNamesOptions
{
    public required HashSet<string> KnownIdentifierNames { get; init; }
    public string[]? IgnoreCaptureGroups { get; init; }
    public Regex? IgnoreIdentifierPattern { get; init; }
    public string[]? IgnorePrefixes { get; init; }
    public string[]? IgnorePostfixes { get; init; }

    // Convenience: empty options (no known identifiers, no ignores)
    public static VariableNamesOptions Empty { get; } = new()
    {
        KnownIdentifierNames = [],
        IgnoreCaptureGroups = null,
        IgnoreIdentifierPattern = null,
        IgnorePrefixes = null,
        IgnorePostfixes = null
    };

    public bool IsEmpty => KnownIdentifierNames.Count == 0;
}
