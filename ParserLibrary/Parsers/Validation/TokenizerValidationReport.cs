using FluentValidation.Results;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class TokenizerValidationReport : CheckResult
{
    public string Expression { get; init; } = string.Empty;

    public ParenthesisCheckResult? ParenthesesResult { get; init; }
    
    public VariableNamesCheckResult? VariableNamesResult { get; init; }

    public override bool IsSuccess => (ParenthesesResult?.IsSuccess ?? true) && (VariableNamesResult?.IsSuccess ?? true);

    public override IList<ValidationFailure> GetValidationFailures()
    {
        List<ValidationFailure> failures = [];
        if (ParenthesesResult is not null && !ParenthesesResult.IsSuccess)
        {
            failures.AddRange(ParenthesesResult.GetValidationFailures());
        }
        if (VariableNamesResult is not null && !VariableNamesResult.IsSuccess)
        {
            failures.AddRange(VariableNamesResult.GetValidationFailures());
        }
        return failures;
    }   

    public static TokenizerValidationReport Success { get => new(); }
}
