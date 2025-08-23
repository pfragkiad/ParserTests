namespace ParserLibrary.Parsers.Interfaces;

public interface IStatefulParserFactory
{
    /// <summary>
    /// Creates a new StatefulParser instance with the specified expression
    /// </summary>
    /// <typeparam name="TParser">The type of StatefulParser to create</typeparam>
    /// <param name="expression">The expression to parse</param>
    /// <returns>A new StatefulParser instance configured with the expression</returns>
    TParser Create<TParser>(
        string? expression,
        Dictionary<string, object?>? variables = null) where TParser : StatefulParser;
}
