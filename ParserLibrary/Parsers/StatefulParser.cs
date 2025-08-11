using Microsoft.Extensions.DependencyInjection;
using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers;

/// <summary>
/// This class can be use for a single evaluation, not for parallel evaluations, because the nodeValueDictionary and stack fields keep the state of the currently evaluated expression.
/// </summary>
public class StatefulParser : Parser, IStatefulParser
{

    //created for simplifying and caching dictionaries
    protected internal Dictionary<Node<Token>, object?> _nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected Stack<Token> _stack = [];

    protected List<Token> _infixTokens = [];
    protected List<Token> _postfixTokens = [];

    public StatefulParser(ILogger<StatefulParser> logger, IOptions<TokenizerOptions> options,
        string? expression = null)
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


    #endregion

    #region Validation checks

    public bool AreParenthesesMatched() =>
        Expression is null || AreParenthesesMatched(Expression!);

    public ParenthesisCheckResult CheckParentheses() =>
        CheckParentheses(_infixTokens);

    public List<string> GetVariableNames() =>
        GetVariableNames(_infixTokens);

    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> identifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes) =>
        CheckVariableNames(_infixTokens, identifierNames, ignorePrefixes, ignorePostfixes);

    public VariableNamesCheckResult CheckVariableNames(
       HashSet<string> identifierNames,
       Regex? ignoreIdentifierPattern = null) =>
       CheckVariableNames(_infixTokens, identifierNames, ignoreIdentifierPattern);


    public VariableNamesCheckResult CheckVariableNames(
        HashSet<string> identifierNames,
        string[] ignoreCaptureGroups) =>
        CheckVariableNames(_infixTokens, identifierNames, ignoreCaptureGroups);

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
