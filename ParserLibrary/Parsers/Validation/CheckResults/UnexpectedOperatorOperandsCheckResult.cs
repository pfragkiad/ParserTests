using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public readonly struct AdjacentOperandsViolation
{
    public string LeftToken { get; init; }
    public int LeftPosition { get; init; }  // 1-based

    public string RightToken { get; init; }
    public int RightPosition { get; init; } // 1-based
}

public sealed class UnexpectedOperatorOperandsCheckResult : CheckResult
{
    public List<AdjacentOperandsViolation> Violations { get; init; } = [];

    public override bool IsSuccess => base.IsSuccess && Violations.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        return [
            .. base.GetValidationFailures(),
            .. Violations.Select(v =>
            new ValidationFailure("Formula.AdjacentOperands",
                $"Missing operator between '{v.LeftToken}' (pos {v.LeftPosition}) and '{v.RightToken}' (pos {v.RightPosition})."))];
    }

    public static UnexpectedOperatorOperandsCheckResult Success => new();
}