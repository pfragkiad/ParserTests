using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public readonly struct FunctionArguments
{
    public string FunctionName { get; init; }

    public int Position { get; init; }

    public int? ExpectedArgumentsCount { get; init; }

    public int? MinExpectedArgumentsCount { get; init; }

    public int? ActualArgumentsCount { get; init; }
}

public class FunctionArgumentsCountCheckResult : CheckResult
{
    public List<FunctionArguments> ValidFunctions { get; init; } = [];
    public List<FunctionArguments> InvalidFunctions { get; init; } = [];

    public override bool IsSuccess => base.IsSuccess && InvalidFunctions.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        return [
            .. base.GetValidationFailures(),
            .. InvalidFunctions.Select(f => new ValidationFailure("Formula.FunctionArgumentsCount", $"Function '{f.FunctionName}' at position {f.Position} expects {f.ExpectedArgumentsCount} argument(s), but got {f.ActualArgumentsCount}."))
        ];
    }
}

public class EmptyFunctionArgumentsCheckResult : CheckResult
{
    public List<FunctionArguments> ValidFunctions { get; init; } = [];

    public List<FunctionArguments> InvalidFunctions { get; init; } = [];

    public override bool IsSuccess => base.IsSuccess && InvalidFunctions.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        return [
            .. base.GetValidationFailures(),
            .. InvalidFunctions.Select(f => new ValidationFailure("Formula.EmptyFunctionArguments", $"Function '{f.FunctionName}' at position {f.Position} has empty argument(s)."))
        ];
    }
}


