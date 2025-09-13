using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class ParserValidationReport
{
    public string Expression { get; set; } = string.Empty;

    // Tokenizer pre-check
    public bool ParenthesesMatched { get; set; }
    public ParenthesisCheckResult? ParenthesesDetail { get; set; }

    // Parser-level checks (optional)
    public FunctionNamesCheckResult? FunctionNames { get; set; }
    public InvalidOperatorsCheckResult? Operators { get; set; }
    public InvalidArgumentSeparatorsCheckResult? ArgumentSeparators { get; set; }
    public FunctionArgumentsCountCheckResult? FunctionArgumentsCount { get; set; }
    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArguments { get; set; }
}