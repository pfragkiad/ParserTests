using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class TokenizerValidationReport
{
    public string Expression { get; init; } = string.Empty;
    public bool ParenthesesMatched { get; init; }
    public ParenthesisErrorCheckResult? ParenthesesDetail { get; init; }
    public VariableNamesCheckResult? VariableNames { get; init; }
}
