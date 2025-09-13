namespace ParserLibrary.Parsers.Validation;

public sealed class VariableNamesOptions
{
    public required HashSet<string> KnownIdentifierNames { get; init; }
    public string[]? IgnoreCaptureGroups { get; init; }
    public Regex? IgnoreIdentifierPattern { get; init; }
    public string[]? IgnorePrefixes { get; init; }
    public string[]? IgnorePostfixes { get; init; }

    //public static VariableNamesOptions FromKnownIdentifierNamesOnly(HashSet<string> knownIdentifierNames) => new()
    //{
    //    KnownIdentifierNames = knownIdentifierNames,
    //    IgnoreCaptureGroups = [],
    //    IgnoreIdentifierPattern = null,
    //    IgnorePrefixes = [],
    //    IgnorePostfixes = []
    //};
}
