using FluentValidation.Results;

namespace ParserLibrary.Tokenizers;

public readonly struct ParenthesisCheckResult
{
    public List<int> UnmatchedClosed { get; init; }

    public List<int> UnmatchedOpen { get; init; }

    public bool IsSuccess => UnmatchedClosed.Count == 0 && UnmatchedOpen.Count == 0;

    public IList<ValidationFailure> GetValidationFailures()
    {
        //report in the order of the position of the unmatched parenthesis
        List<(int, ValidationFailure)> failuresByPosition = [];

        foreach (var unmatched in UnmatchedOpen)
        {
            //_logger.LogError("Unmatched open parenthesis at position {position}", unmatched);
            //failures.Add(new ValidationFailure("Formula", $"Unmatched open parenthesis at position {unmatched}."));
            failuresByPosition.Add((unmatched, new ValidationFailure("Formula", $"Unmatched open parenthesis at position {unmatched}.")));
        }
        foreach (var unmatched in UnmatchedClosed)
        {
            //_logger.LogError("Unmatched close parenthesis at position {position}", unmatched);
            //failures.Add(new ValidationFailure("Formula", $"Unmatched close parenthesis at position {unmatched}."));
            failuresByPosition.Add((unmatched, new ValidationFailure("Formula", $"Unmatched close parenthesis at position {unmatched}.")));
        }
        // Sort failures by position and return only failures 
        return [.. failuresByPosition.OrderBy(e => e.Item1).Select( e => e.Item2)];
    }

} 