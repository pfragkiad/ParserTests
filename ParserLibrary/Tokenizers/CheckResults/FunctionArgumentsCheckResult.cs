using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

public readonly struct FunctionArgumentCheckResult
{
    public string FunctionName { get; init; }

    public int Position { get; init; }

    public int? ExpectedArgumentsCount { get; init; }

    public int? ActualArgumentsCount { get; init; }
}

public class FunctionArgumentsCheckResult : CheckResult
{
    public List<FunctionArgumentCheckResult> ValidFunctions { get; init; } = [];
    public List<FunctionArgumentCheckResult> InvalidFunctions { get; init; } = [];

    public override bool IsSuccess => InvalidFunctions.Count==0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        //return [.. InvalidFunctions.Select(name => new ValidationFailure("Formula", $"Invalid function arguments count: {name}"))];
        return [.. InvalidFunctions.Select(f => new ValidationFailure("Formula", $"Function '{f.FunctionName}' at position {f.Position} expects {f.ExpectedArgumentsCount} argument(s), but got {f.ActualArgumentsCount}."))];
    }

}

public class EmptyFunctionArgumentsCheckResult : CheckResult
{
    public List<FunctionArgumentCheckResult> ValidFunctions { get; init; } = [];

    public List<FunctionArgumentCheckResult> InvalidFunctions { get; init; } = [];

    public override bool IsSuccess => InvalidFunctions.Count==0;
    public override IList<ValidationFailure> GetValidationFailures()
    {
        return [.. InvalidFunctions.Select(f => new ValidationFailure("Formula", $"Function '{f.FunctionName}' at position {f.Position} has empty argument(s)."))];
    }
}