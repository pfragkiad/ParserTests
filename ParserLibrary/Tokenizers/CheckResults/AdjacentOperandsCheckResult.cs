using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

public readonly struct AdjacentOperandsViolation
{
    public string LeftToken { get; init; }
    public int LeftPosition { get; init; }  // 1-based

    public string RightToken { get; init; }
    public int RightPosition { get; init; } // 1-based
}

public sealed class AdjacentOperandsCheckResult : CheckResult
{
    public List<AdjacentOperandsViolation> Violations { get; init; } = [];

    public override bool IsSuccess => Violations.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        return [.. Violations.Select(v =>
            new ValidationFailure(
                "Formula",
                $"Missing operator between '{v.LeftToken}' (pos {v.LeftPosition}) and '{v.RightToken}' (pos {v.RightPosition})."))];
    }

    public static AdjacentOperandsCheckResult Success => new();
}