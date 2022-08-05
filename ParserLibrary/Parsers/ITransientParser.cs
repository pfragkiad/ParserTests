namespace ParserLibrary.Parsers
{
    public interface ITransientParser
    {
        object Evaluate(string s, Dictionary<string, object> variables = null);
    }
}