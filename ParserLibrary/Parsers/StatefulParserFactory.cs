using Microsoft.Extensions.DependencyInjection;
using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers;

/// <summary>
/// Factory for creating StatefulParser instances with expressions
/// </summary>
public class StatefulParserFactory : IStatefulParserFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StatefulParserFactory> _logger;
    private readonly IOptions<TokenizerOptions> _options;

    public StatefulParserFactory(
        IServiceProvider serviceProvider,
        ILogger<StatefulParserFactory> logger,
        IOptions<TokenizerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Creates a new StatefulParser instance with the specified expression
    /// </summary>
    /// <typeparam name="TStatefulParser">The type of StatefulParser to create</typeparam>
    /// <param name="expression">The expression to parse</param>
    /// <returns>A new StatefulParser instance configured with the expression</returns>
    public TStatefulParser Create<TStatefulParser>(
        string? expression,
        Dictionary<string, object?>? variables = null) where TStatefulParser : StatefulParser
    {
        try
        {
            // Create the parser instance using reflection since we need to pass the expression to the constructor
            var parserInstance = (TStatefulParser)Activator.CreateInstance(typeof(TStatefulParser), 
                _serviceProvider.GetRequiredService<ILogger<TStatefulParser>>(), 
                _options,
                expression,
                variables)!;

            _logger.LogDebug("Created StatefulParser of type {ParserType} with expression: {Expression}", typeof(TStatefulParser).Name, expression);
            
            return parserInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create StatefulParser of type {ParserType} with expression: {Expression}", typeof(TStatefulParser).Name, expression);
            throw;
        }
    }
}
