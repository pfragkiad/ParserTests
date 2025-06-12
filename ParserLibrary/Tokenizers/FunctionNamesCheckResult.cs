using FluentValidation.Results;

namespace ParserLibrary.Tokenizers;

public readonly struct FunctionNamesCheckResult
{
    public List<string> UnmatchedFunctionNames { get; init; }

    public bool IsSuccess => UnmatchedFunctionNames.Count == 0;

    public IList<ValidationFailure> GetValidationFailures()
    {
        //report in the order of the position of the unmatched function names
        return [.. UnmatchedFunctionNames.Select(name => new ValidationFailure("Formula", $"Unmatched function name: {name}"))];
    }


}
