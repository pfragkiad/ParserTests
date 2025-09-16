using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public class ParenthesisCheckResult : CheckResult
{
    public List<int> UnmatchedClosed { get; init; } = [];

    public List<int> UnmatchedOpen { get; init; }  =[];

    public override bool IsSuccess => base.IsSuccess && UnmatchedClosed.Count == 0 && UnmatchedOpen.Count == 0;

    public static ParenthesisCheckResult Success { get => new(); }

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        //report in the order of the position of the unmatched parenthesis
        List<(int, ValidationFailure)> failuresByPosition = [];

        foreach (var unmatched in UnmatchedOpen)
        {
            failuresByPosition.Add((unmatched, new ValidationFailure("Formula.Parentheses", $"Unmatched open parenthesis at position {unmatched+1}.")));
        }
        foreach (var unmatched in UnmatchedClosed)
        {
            failuresByPosition.Add((unmatched, new ValidationFailure("Formula.Parentheses", $"Unmatched close parenthesis at position {unmatched+1}.")));
        }

        return [
            .. base.GetValidationFailures(),
            .. failuresByPosition.OrderBy(e => e.Item1).Select(e => e.Item2)
        ];
    }
}
