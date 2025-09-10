namespace ParserLibrary.Parsers.Validation;

public sealed class VariableNamesOptions
{
    public required HashSet<string> IdentifierNames { get; init; }
    public string[]? IgnoreCaptureGroups { get; init; }
    public Regex? IgnoreIdentifierPattern { get; init; }
    public string[]? IgnorePrefixes { get; init; }
    public string[]? IgnorePostfixes { get; init; }
}
