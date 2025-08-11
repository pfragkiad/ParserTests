using FluentValidation.Results;

namespace ParserLibrary.Parsers;

public interface IStatefulParser : IParser
{
    string? Expression { get; set; }

    Dictionary<string, object?> Variables { get; set; }

    object? Evaluate();

    object? Evaluate(Dictionary<string, object?>? variables = null);


    Type EvaluateType();

    Type EvaluateType(Dictionary<string, object?>? variables = null);

    List<ValidationFailure> GetValidationFailures();
}