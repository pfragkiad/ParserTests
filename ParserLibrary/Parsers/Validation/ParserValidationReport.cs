using FluentValidation.Results;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class ParserValidationReport : TokenizerValidationReport
{
    public override bool IsSuccess =>
        base.IsSuccess &&
        (FunctionNamesResult?.IsSuccess ?? true) &&
        (EmptyFunctionArgumentsResult?.IsSuccess ?? true) &&
        (OperatorOperandsResult?.IsSuccess ?? true) &&
        (OrphanArgumentSeparatorsResult?.IsSuccess ?? true) &&
        (FunctionArgumentsCountResult?.IsSuccess ?? true)
        ;

    // Parser-level checks (optional)
    public FunctionNamesCheckResult? FunctionNamesResult { get; set; }

    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArgumentsResult { get; set; }

    public InvalidOperatorsCheckResult? OperatorOperandsResult { get; set; }

    public InvalidArgumentSeparatorsCheckResult? OrphanArgumentSeparatorsResult { get; set; }
    public FunctionArgumentsCountCheckResult? FunctionArgumentsCountResult { get; set; }

    public override IList<ValidationFailure> GetValidationFailures()
    {
        List<ValidationFailure> failures = [];

        // Include tokenizer-level failures
        var baseFailures = base.GetValidationFailures();
        if (baseFailures.Count > 0)
        {
            failures.AddRange(baseFailures);
        }

        // Include parser-level failures (null treated as success)
        if (FunctionNamesResult is { IsSuccess: false })
        {
            failures.AddRange(FunctionNamesResult.GetValidationFailures());
        }

        if (OperatorOperandsResult is { IsSuccess: false })
        {
            failures.AddRange(OperatorOperandsResult.GetValidationFailures());
        }

        if (OrphanArgumentSeparatorsResult is { IsSuccess: false })
        {
            failures.AddRange(OrphanArgumentSeparatorsResult.GetValidationFailures());
        }

        if (FunctionArgumentsCountResult is { IsSuccess: false })
        {
            failures.AddRange(FunctionArgumentsCountResult.GetValidationFailures());
        }

        if (EmptyFunctionArgumentsResult is { IsSuccess: false })
        {
            failures.AddRange(EmptyFunctionArgumentsResult.GetValidationFailures());
        }

        return failures;
    }
}