using FluentValidation.Results;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class ParserValidationReport : TokenizerValidationReport
{
    public override bool IsSuccess =>
        base.IsSuccess &&
        (AdjacentOperandsResult?.IsSuccess ?? true) &&
        (FunctionNamesResult?.IsSuccess ?? true) &&
        (EmptyFunctionArgumentsResult?.IsSuccess ?? true) &&
        (BinaryOperatorOperandsResult?.IsSuccess ?? true) &&
        (UnaryOperatorOperandsResult?.IsSuccess ?? true) &&
        (OrphanArgumentSeparatorsResult?.IsSuccess ?? true) &&
        (FunctionArgumentsCountResult?.IsSuccess ?? true)
        ;

    // NEW
    public AdjacentOperandsCheckResult? AdjacentOperandsResult { get; set; }

    // Parser-level checks (optional)
    public FunctionNamesCheckResult? FunctionNamesResult { get; set; }

    public FunctionArgumentsCountCheckResult? FunctionArgumentsCountResult { get; set; }

    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArgumentsResult { get; set; }

    public InvalidOperatorsCheckResult? BinaryOperatorOperandsResult { get; set; }

    public InvalidUnaryOperatorsCheckResult? UnaryOperatorOperandsResult { get; set; } // NEW

    public InvalidArgumentSeparatorsCheckResult? OrphanArgumentSeparatorsResult { get; set; }

    public override IList<ValidationFailure> GetValidationFailures()
    {
        List<ValidationFailure> failures = [];

        // Include tokenizer-level failures
        var baseFailures = base.GetValidationFailures();
        if (baseFailures.Count > 0)
            failures.AddRange(baseFailures);

        if (AdjacentOperandsResult is not null && !AdjacentOperandsResult.IsSuccess)
            failures.AddRange(AdjacentOperandsResult.GetValidationFailures());

        if (FunctionNamesResult is not null && !FunctionNamesResult.IsSuccess)
            failures.AddRange(FunctionNamesResult.GetValidationFailures());

        if (BinaryOperatorOperandsResult is not null && !BinaryOperatorOperandsResult.IsSuccess)
            failures.AddRange(BinaryOperatorOperandsResult.GetValidationFailures());

        if (UnaryOperatorOperandsResult is not null && !UnaryOperatorOperandsResult.IsSuccess) // NEW
            failures.AddRange(UnaryOperatorOperandsResult.GetValidationFailures());

        if (OrphanArgumentSeparatorsResult is not null && !OrphanArgumentSeparatorsResult.IsSuccess)
            failures.AddRange(OrphanArgumentSeparatorsResult.GetValidationFailures());

        if (FunctionArgumentsCountResult is { IsSuccess: false })
            failures.AddRange(FunctionArgumentsCountResult.GetValidationFailures());

        if (EmptyFunctionArgumentsResult is { IsSuccess: false })
            failures.AddRange(EmptyFunctionArgumentsResult.GetValidationFailures());

        return failures;
    }
}