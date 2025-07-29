namespace ParserLibrary.Parsers;

public interface IStatefulParser : IParser
{
    string? Expression { get; set; }

    object? Evaluate(Dictionary<string, object?>? variables = null);


    Type EvaluateType(Dictionary<string, object?>? variables = null);
}