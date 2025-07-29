namespace ParserLibrary.Parsers;

public interface IStatefulParserFactory
{
    /// <summary>
    /// Creates a new StatefulParser instance with the specified expression
    /// </summary>
    /// <typeparam name="TParser">The type of StatefulParser to create</typeparam>
    /// <param name="expression">The expression to parse</param>
    /// <returns>A new StatefulParser instance configured with the expression</returns>
    TParser Create<TParser>(string expression) where TParser : StatefulParser;
}

public interface IStatefulParser : IParser
{
    string? Expression { get; set; }

    object? Evaluate(Dictionary<string, object?>? variables = null);


    Type EvaluateType(Dictionary<string, object?>? variables = null);
}