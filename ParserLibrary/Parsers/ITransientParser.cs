

namespace ParserLibrary.Parsers;

public interface ITransientParser : IParserBase
{

    object Evaluate(string s, Dictionary<string, object>? variables = null);
    Type EvaluateType(string s, Dictionary<string, object>? variables = null);


}