

namespace ParserLibrary.Parsers
{
    public interface ITransientParser
    {
        ITokenizer Tokenizer { get; }

        object Evaluate(string s, Dictionary<string, object>? variables = null);
        Type EvaluateType(string s, Dictionary<string, object>? variables = null);

        void RegisterFunction(string definition);

    }
}