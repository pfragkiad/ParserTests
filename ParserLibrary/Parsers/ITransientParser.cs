namespace ParserLibrary.Parsers
{
    public interface ITransientParser
    {
        bool AreParenthesesMatched(string expression);
        object Evaluate(string s, Dictionary<string, object>? variables = null);
        void RegisterFunction(string definition);
    }
}