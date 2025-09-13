using FluentValidation.Results;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Validation;

public sealed class ExpressionValidationReport
{
    public string Expression { get; set; } = string.Empty;

    // Tokenizer-level
    public bool ParenthesesMatched { get; set; }
    public ParenthesisCheckResult? ParenthesisCheck { get; set; }
    public VariableNamesCheckResult? VariableNames { get; set; }

    // Parser-level
    public FunctionNamesCheckResult? FunctionNames { get; set; }
    public InvalidOperatorsCheckResult? Operators { get; set; }
    public InvalidArgumentSeparatorsCheckResult? ArgumentSeparators { get; set; }
    public FunctionArgumentsCountCheckResult? FunctionArgumentsCount { get; set; }
    public EmptyFunctionArgumentsCheckResult? EmptyFunctionArguments { get; set; }

    public List<ValidationFailure> ToValidationFailures()
    {
        var failures = new List<ValidationFailure>();

        if (ParenthesisCheck is not null)
            failures.AddRange(ParenthesisCheck.GetValidationFailures());

        if (FunctionNames is not null && !FunctionNames.IsSuccess)
            failures.AddRange(FunctionNames.GetValidationFailures());

        if (VariableNames is not null && !VariableNames.IsSuccess)
            failures.AddRange(VariableNames.GetValidationFailures());

        if (Operators is not null && !Operators.IsSuccess)
            failures.AddRange(Operators.GetValidationFailures());

        if (ArgumentSeparators is not null && !ArgumentSeparators.IsSuccess)
            failures.AddRange(ArgumentSeparators.GetValidationFailures());

        if (FunctionArgumentsCount is not null && !FunctionArgumentsCount.IsSuccess)
            failures.AddRange(FunctionArgumentsCount.GetValidationFailures());

        if (EmptyFunctionArguments is not null && !EmptyFunctionArguments.IsSuccess)
            failures.AddRange(EmptyFunctionArguments.GetValidationFailures());

        return failures;
    }
}