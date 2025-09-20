namespace ParserLibrary.Parsers.Validation;

public enum IgnoreMode { None, CaptureGroups, Pattern, PrefixPostfix }

public sealed class VariableNamesOptions
{
    public required HashSet<string> KnownIdentifierNames { get; init; }
    public HashSet<string>? IgnoreCaptureGroups { get; init; }
    public Regex? IgnoreIdentifierPattern { get; init; }
    public HashSet<string>? IgnorePrefixes { get; init; }
    public HashSet<string>? IgnorePostfixes { get; init; }

    public IgnoreMode IgnoreMode =>
        IgnoreCaptureGroups is not null ? IgnoreMode.CaptureGroups
        : IgnoreIdentifierPattern is not null ? IgnoreMode.Pattern
        : (IgnorePrefixes is not null || IgnorePostfixes is not null) ? IgnoreMode.PrefixPostfix
        : IgnoreMode.None;

    // Convenience: empty options (no known identifiers, no ignores)
    public static VariableNamesOptions Empty { get; } = new()
    {
        KnownIdentifierNames = [],
        IgnoreCaptureGroups = null,
        IgnoreIdentifierPattern = null,
        IgnorePrefixes = null,
        IgnorePostfixes = null
    };

    public static VariableNamesOptions FromKnownNames(IEnumerable<string> knownNames) => new()
    {
        KnownIdentifierNames = knownNames is HashSet<string> hs ? hs : [.. knownNames],
        IgnoreCaptureGroups = null,
        IgnoreIdentifierPattern = null,
        IgnorePrefixes = null,
        IgnorePostfixes = null
    };

    public bool IsEmpty => KnownIdentifierNames.Count == 0;
}
