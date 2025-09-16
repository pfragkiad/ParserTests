using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public class NamesCheckResult : CheckResult
{
    public List<string> MatchedNames { get; init; } = [];
    public List<string> UnmatchedNames { get; init; } = [];

    public List<string> IgnoredNames { get; init; } = [];   

    public override bool IsSuccess => base.IsSuccess && UnmatchedNames.Count == 0;

    public virtual string Category { get; } = "";

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        string propertyName = $"Formula.{Category}Names";
        return [
            .. base.GetValidationFailures(),
            .. UnmatchedNames.Select(name => new ValidationFailure(propertyName, $"Unmatched {Category} name: {name}"))
        ];
    }
}

public class VariableNamesCheckResult : NamesCheckResult
{
    public override string Category => "Variable";
}

public class FunctionNamesCheckResult : NamesCheckResult
{
    public override string Category => "Function";
}
