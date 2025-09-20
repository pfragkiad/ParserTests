using FluentValidation.Results;
using ParserLibrary.Parsers.Validation.CheckResults;

namespace ParserLibrary.Parsers.Validation.Reports;

public class TokenizerValidationReport : CheckResult
{
    public string Expression { get; init; } = string.Empty;

    public ParenthesisCheckResult? ParenthesesResult { get; set; }

    public VariableNamesCheckResult? VariableNamesResult { get; set; }

    public FunctionNamesCheckResult? FunctionNamesResult { get; set; }

    public UnexpectedOperatorOperandsCheckResult? UnexpectedOperatorOperandsResult { get; set; }

    public List<Token>? InfixTokens { get; set; }


    public override bool IsSuccess =>
        base.IsSuccess &&
        (ParenthesesResult?.IsSuccess ?? true) &&
        (VariableNamesResult?.IsSuccess ?? true) &&
        (UnexpectedOperatorOperandsResult?.IsSuccess ?? true) &&
        (FunctionNamesResult?.IsSuccess ?? true);

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        var failures = new List<ValidationFailure>(capacity: 8);
        var baseFailures = base.GetValidationFailures();
        if (baseFailures.Count > 0) failures.AddRange(baseFailures);

        if (ParenthesesResult is { IsSuccess: false })
            failures.AddRange(ParenthesesResult.GetValidationFailures());

        if (VariableNamesResult is { IsSuccess: false })
            failures.AddRange(VariableNamesResult.GetValidationFailures());

        if (FunctionNamesResult is { IsSuccess: false })
            failures.AddRange(FunctionNamesResult.GetValidationFailures());

        if (UnexpectedOperatorOperandsResult is { IsSuccess: false })
            failures.AddRange(UnexpectedOperatorOperandsResult.GetValidationFailures());


        return failures;
    }

}
