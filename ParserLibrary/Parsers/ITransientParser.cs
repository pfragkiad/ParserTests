

namespace ParserLibrary.Parsers;

public interface ITransientParser : IParser
{

    object Evaluate(string s, Dictionary<string, object>? variables = null);
    Type EvaluateType(string s, Dictionary<string, object>? variables = null);


}