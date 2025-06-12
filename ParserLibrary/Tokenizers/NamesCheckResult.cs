using FluentValidation.Results;

namespace ParserLibrary.Tokenizers;

public class NamesCheckResult
{
    public List<string> MatchedNames { get; init; } = [];
    public List<string> UnmatchedNames { get; init; } = [];

    public List<string> IgnoredNames { get; init; } = [];   


    public bool IsSuccess => UnmatchedNames.Count == 0;

    public virtual string NameCategory { get; } = "";

    public IList<ValidationFailure> GetValidationFailures()
    {
        //report in the order of the position of the unmatched identifier names
        return [.. UnmatchedNames.Select(name => new ValidationFailure("Formula", $"Unmatched {NameCategory} name: {name}"))];
    }
}

public class VariableNamesCheckResult : NamesCheckResult
{
    public override string NameCategory => "variable";

}
public class FunctionNamesCheckResult : NamesCheckResult
{
    public override string NameCategory => "function";
}