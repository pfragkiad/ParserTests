using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public readonly struct FunctionArguments
{
    public string FunctionName { get; init; }

    public int Position { get; init; }

    // Legacy single fixed expected count
        public int? ExpectedArgumentsCount { get; init; }

    // Legacy min/max window
    public int? MinExpectedArgumentsCount { get; init; }
    public int? MaxExpectedArgumentsCount { get; init; }

    // New: when FunctionInformation provides multiple fixed alternatives via syntaxes
    public IList<int>? AllowedFixedCounts { get; init; }

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
            .. InvalidFunctions.Select(f =>
            {
                // Build an informative message based on what metadata is available
                string expectation = BuildExpectationText(f);
                return new ValidationFailure(
                    "Formula.FunctionArgumentsCount",
                    $"Function '{f.FunctionName}' at position {f.Position} {expectation}, but got {f.ActualArgumentsCount}.");
            })
        ];
    }

    private static string BuildExpectationText(FunctionArguments f)
    {
        // 1) Exact fixed count
        if (f.ExpectedArgumentsCount is int exact)
            return $"expects exactly {exact} argument(s)";

        // 2) Fixed alternatives (syntaxes) with optional min (dynamic)
        bool hasFixedSet = f.AllowedFixedCounts is { Count: > 0 };
        bool hasMin = f.MinExpectedArgumentsCount is int minOnly && f.MaxExpectedArgumentsCount is null;

        if (hasFixedSet && hasMin)
            return $"expects one of [{string.Join(", ", f.AllowedFixedCounts!)}] argument(s) or at least {f.MinExpectedArgumentsCount} argument(s)";

        if (hasFixedSet)
            return $"expects one of [{string.Join(", ", f.AllowedFixedCounts!)}] argument(s)";

        if (hasMin)
            return $"expects at least {f.MinExpectedArgumentsCount} argument(s)";

        // 3) Min/max window (legacy)
        if (f.MinExpectedArgumentsCount is int min && f.MaxExpectedArgumentsCount is int max)
            return $"expects between {min} and {max} argument(s)";

        // Fallback
        return "has invalid argument count";
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


