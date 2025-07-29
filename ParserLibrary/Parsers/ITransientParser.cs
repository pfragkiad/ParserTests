namespace ParserLibrary.Parsers;

public interface ITransientParser : IParser
{
    object? Evaluate(Dictionary<string, object?>? variables = null);


    Type EvaluateType(Dictionary<string, object?>? variables = null);
}