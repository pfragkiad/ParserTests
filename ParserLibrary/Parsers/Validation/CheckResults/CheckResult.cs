using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public abstract class CheckResult
{
    public Exception? Exception { get; set; } = null;

    public virtual bool IsSuccess { get => Exception is null; }

    public virtual IList<ValidationFailure> GetValidationFailures()
    {
        if (Exception is null) return [];
        return [new ValidationFailure("Formula.Exception", Exception.Message)];
    }

    public override string ToString() =>
        IsSuccess ? "Success" :
            $"Failed with {GetValidationFailures().Count} validation failures";
}
