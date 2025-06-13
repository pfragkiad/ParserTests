using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

public class InvalidArgumentSeparatorsCheckResult : CheckResult
{
    public List<int> InvalidPositions { get; init; } = [];

    public List<int> ValidPositions { get; init; } = [];

    public override bool IsSuccess => InvalidPositions.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        return [.. InvalidPositions.Select(pos => new ValidationFailure("Formula", $"Invalid argument separator at position {pos}."))];
    }
}
