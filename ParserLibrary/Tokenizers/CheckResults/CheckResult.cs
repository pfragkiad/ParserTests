using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

public abstract class CheckResult
{
    public abstract bool IsSuccess { get; }

    public abstract IList<ValidationFailure> GetValidationFailures();

    public override string ToString()  =>
        IsSuccess ?  "Success" :
            $"Failed with {GetValidationFailures().Count} validation failures";
}
