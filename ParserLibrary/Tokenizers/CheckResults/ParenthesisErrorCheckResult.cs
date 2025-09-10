using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

public class ParenthesisErrorCheckResult : CheckResult
{
    public List<int> UnmatchedClosed { get; init; } = [];

    public List<int> UnmatchedOpen { get; init; }  =[];

    public override bool IsSuccess => UnmatchedClosed.Count == 0 && UnmatchedOpen.Count == 0;

    public static ParenthesisErrorCheckResult Success { get => new(); }

    public override IList<ValidationFailure> GetValidationFailures()
    {
        //report in the order of the position of the unmatched parenthesis
        List<(int, ValidationFailure)> failuresByPosition = [];

        foreach (var unmatched in UnmatchedOpen)
        {
            //_logger.LogError("Unmatched open parenthesis at position {position}", unmatched);
            //failures.Add(new ValidationFailure("Formula", $"Unmatched open parenthesis at position {unmatched}."));
            failuresByPosition.Add((unmatched, new ValidationFailure("Formula", $"Unmatched open parenthesis at position {unmatched+1}.")));
        }
        foreach (var unmatched in UnmatchedClosed)
        {
            //_logger.LogError("Unmatched close parenthesis at position {position}", unmatched);
            //failures.Add(new ValidationFailure("Formula", $"Unmatched close parenthesis at position {unmatched}."));
            failuresByPosition.Add((unmatched, new ValidationFailure("Formula", $"Unmatched close parenthesis at position {unmatched+1}.")));
        }
        // Sort failures by position and return only failures 
        return [.. failuresByPosition.OrderBy(e => e.Item1).Select( e => e.Item2)];
    }

} 
