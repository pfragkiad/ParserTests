using FluentValidation.Results;
using ParserLibrary.Parsers.Validation.CheckResults;

namespace ParserLibrary.Parsers.Validation.Reports;

public sealed class ParserValidationReport : TokenizerValidationReport
{
    public override bool IsSuccess =>
        base.IsSuccess &&
        (UnexpectedOperatorOperandsResult?.IsSuccess ?? true) &&
        (FunctionNamesResult?.IsSuccess ?? true) &&
        (EmptyFunctionArgumentsResult?.IsSuccess ?? true) &&
        (BinaryOperatorOperandsResult?.IsSuccess ?? true) &&
        (UnaryOperatorOperandsResult?.IsSuccess ?? true) &&
        (OrphanArgumentSeparatorsResult?.IsSuccess ?? true) &&
        (FunctionArgumentsCountResult?.IsSuccess ?? true);

    // NEW
    public UnexpectedOperatorOperandsCheckResult? UnexpectedOperatorOperandsResult { get; set; }

    // Parser-level checks (optional)
    public FunctionNamesCheckResult? FunctionNamesResult { get; set; }
    public FunctionArgumentsCountCheckResult? FunctionArgumentsCountResult { get; set; }
    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArgumentsResult { get; set; }
    public InvalidOperatorsCheckResult? BinaryOperatorOperandsResult { get; set; }
    public InvalidUnaryOperatorsCheckResult? UnaryOperatorOperandsResult { get; set; } // NEW
    public InvalidArgumentSeparatorsCheckResult? OrphanArgumentSeparatorsResult { get; set; }

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];


        var failures = new List<ValidationFailure>(capacity: 16);
        var baseFailures = base.GetValidationFailures();
        if (baseFailures.Count > 0) failures.AddRange(baseFailures);

        if (UnexpectedOperatorOperandsResult is { IsSuccess: false })
            failures.AddRange(UnexpectedOperatorOperandsResult.GetValidationFailures());

        if (FunctionNamesResult is { IsSuccess: false })
            failures.AddRange(FunctionNamesResult.GetValidationFailures());

        if (BinaryOperatorOperandsResult is { IsSuccess: false })
            failures.AddRange(BinaryOperatorOperandsResult.GetValidationFailures());

        if (UnaryOperatorOperandsResult is { IsSuccess: false })
            failures.AddRange(UnaryOperatorOperandsResult.GetValidationFailures());

        if (OrphanArgumentSeparatorsResult is { IsSuccess: false })
            failures.AddRange(OrphanArgumentSeparatorsResult.GetValidationFailures());

        if (FunctionArgumentsCountResult is { IsSuccess: false })
            failures.AddRange(FunctionArgumentsCountResult.GetValidationFailures());

        if (EmptyFunctionArgumentsResult is { IsSuccess: false })
            failures.AddRange(EmptyFunctionArgumentsResult.GetValidationFailures());

        return failures;
    }
}