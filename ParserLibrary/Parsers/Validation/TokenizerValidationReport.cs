using FluentValidation.Results;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public class TokenizerValidationReport : CheckResult
{
    public string Expression { get; init; } = string.Empty;

    public ParenthesisCheckResult? ParenthesesResult { get; set; }
    
    public VariableNamesCheckResult? VariableNamesResult { get; set; }

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
