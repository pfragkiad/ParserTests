using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

public class NamesCheckResult : CheckResult
{
    public List<string> MatchedNames { get; init; } = [];
    public List<string> UnmatchedNames { get; init; } = [];

    public List<string> IgnoredNames { get; init; } = [];   


    public override bool IsSuccess => UnmatchedNames.Count == 0;

    public virtual string Category { get; } = "";

    public override IList<ValidationFailure> GetValidationFailures()
    {
        //report in the order of the position of the unmatched identifier names
        return [.. UnmatchedNames.Select(name => new ValidationFailure("Formula", $"Unmatched {Category} name: {name}"))];
    }
}

public class VariableNamesCheckResult : NamesCheckResult
{
    public override string Category => "variable";

}
public class FunctionNamesCheckResult : NamesCheckResult
{
    public override string Category => "function";
}
