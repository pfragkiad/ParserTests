using Microsoft.Extensions.DependencyInjection;
using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers;

/// <summary>
/// Factory for creating StatefulParser instances with expressions
/// </summary>
public class StatefulParserFactory : IStatefulParserFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StatefulParserFactory> _logger;
    private readonly IOptions<TokenizerOptions> _options;

    public StatefulParserFactory(IServiceProvider serviceProvider, ILogger<StatefulParserFactory> logger, IOptions<TokenizerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Creates a new StatefulParser instance with the specified expression
    /// </summary>
    /// <typeparam name="TParser">The type of StatefulParser to create</typeparam>
    /// <param name="expression">The expression to parse</param>
    /// <returns>A new StatefulParser instance configured with the expression</returns>
    public TParser Create<TParser>(string expression) where TParser : StatefulParser
    {
        try
        {
            // Create the parser instance using reflection since we need to pass the expression to the constructor
            var parserInstance = (TParser)Activator.CreateInstance(typeof(TParser), 
                _serviceProvider.GetRequiredService<ILogger<TParser>>(), 
                _options, 
                expression)!;

            _logger.LogDebug("Created StatefulParser of type {ParserType} with expression: {Expression}", typeof(TParser).Name, expression);
            
            return parserInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create StatefulParser of type {ParserType} with expression: {Expression}", typeof(TParser).Name, expression);
            throw;
        }
    }
}

/// <summary>
/// This class can be use for a single evaluation, not for parallel evaluations, because the nodeValueDictionary and stack fields keep the state of the currently evaluated expression.
/// </summary>
public class StatefulParser : Parser, IStatefulParser
{

    //created for simplifying and caching dictionaries
    protected internal Dictionary<Node<Token>, object?> _nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected Stack<Token> _stack =[];

    protected List<Token> _infixTokens = [];
    protected List<Token> _postfixTokens = [];

    public StatefulParser(ILogger<StatefulParser> logger, IOptions<TokenizerOptions> options, string? expression = null)
        : base(logger, options)
    {
        //assign expression if not null or whitespace
        if (!string.IsNullOrWhiteSpace(expression))
            Expression = expression;
    }

    //protected TransientParser(ILogger logger, IOptions<TokenizerOptions> options, string expression)
    //: base(logger, options)
    //{ 

    //}

    #region 


    #region Expression fields


    private string? _expression;
    public string? Expression
    {
        get => _expression;
        set
        {
            Reset();

            _expression = value;
            if (string.IsNullOrWhiteSpace(_expression)) return;

            //parses all tokens
            _infixTokens = GetInfixTokens(value!);
            _postfixTokens = GetPostfixTokens(_infixTokens);
        }
    }
    #endregion


    protected void Reset()
    {
        _infixTokens = [];
        _postfixTokens = [];

        _nodeValueDictionary = [];
        _nodeDictionary = [];
        _stack = [];
    }

    //also resets the internal expression
    public override object? Evaluate(string expression, Dictionary<string, object?>? variables = null)
    {
        Expression = expression;
        return Evaluate(variables);
    }

    //also resets the internal expression

    public override Type EvaluateType(string expression, Dictionary<string, object?>? variables = null)
    {
        Expression = expression;
        return EvaluateType(variables);
    }

    public object? Evaluate(Dictionary<string, object?>? variables = null) =>
        Evaluate(_postfixTokens, variables, _stack, _nodeDictionary, _nodeValueDictionary);

    public Type EvaluateType(Dictionary<string, object?>? variables = null) =>
        EvaluateType(_postfixTokens, variables, _stack, _nodeDictionary, _nodeValueDictionary);


    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments() =>
        CheckEmptyFunctionArguments(_nodeDictionary);

    public FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount() =>
        CheckFunctionArgumentsCount(_nodeDictionary);

    public InvalidOperatorsCheckResult CheckOperators() =>
        CheckOperators(_nodeDictionary);


    public InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators() =>
        CheckOrphanArgumentSeparators(_nodeDictionary);




    #endregion


}
