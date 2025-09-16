using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public class InvalidArgumentSeparatorsCheckResult : CheckResult
{
    public List<int> InvalidPositions { get; init; } = [];

    public List<int> ValidPositions { get; init; } = [];

    public override bool IsSuccess => base.IsSuccess && InvalidPositions.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        return [
            .. base.GetValidationFailures(),
            .. InvalidPositions.Select(pos => new ValidationFailure("Formula.ArgumentSeparators", $"Invalid argument separator at position {pos}."))
        ];
    }
}
