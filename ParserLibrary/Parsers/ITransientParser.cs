

namespace ParserLibrary.Parsers
{
    public interface ITransientParser
    {
        bool AreParenthesesMatched(string expression);
        object Evaluate(string s, Dictionary<string, object>? variables = null);
        Type EvaluateType(string s, Dictionary<string, object>? variables = null);
        ParenthesisCheckResult GetUnmatchedParentheses(string expression);
        void RegisterFunction(string definition);
    }
}