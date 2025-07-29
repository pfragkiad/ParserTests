using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers;

/// <summary>
/// This class can be use for a single evaluation, not for parallel evaluations, because the nodeValueDictionary and stack fields keep the state of the currently evaluated expression.
/// </summary>
public class TransientParser : Parser, ITransientParser
{

    //created for simplifying and caching dictionaries
    protected internal Dictionary<Node<Token>, object?> _nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> _nodeDictionary = [];
    protected Stack<Token> _stack = new();



    public TransientParser(ILogger<TransientParser> logger, IOptions<TokenizerOptions> options, string expression) 
        :base(logger, options) 
    { 
        Expresion = expression;
    }

    //protected TransientParser(ILogger logger, IOptions<TokenizerOptions> options, string expression)
    //: base(logger, options)
    //{ 
    
    //}

    #region 


    #region Expression fields

    protected List<Token> _infixTokens = [];
    protected List<Token> _postfixTokens = [];  


    private string? _expression;
    public string? Expresion
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

    //public override object? Evaluate(string s, Dictionary<string, object?>? variables = null)
    //{
    //    Reset();

    //    var postfixTokens = GetPostfixTokens(s);
    //    return base.Evaluate(postfixTokens, variables,stack,nodeDictionary,nodeValueDictionary);
    //}

    //public override Type EvaluateType(string s, Dictionary<string, object?>? variables = null)
    //{
    //    var postfixTokens = GetPostfixTokens(s);
    //    return base.EvaluateType(postfixTokens, variables, stack, nodeDictionary,nodeValueDictionary);
    //}

    public object? Evaluate(Dictionary<string, object?>? variables = null)
    {
        return base.Evaluate(_postfixTokens, variables, _stack, _nodeDictionary, _nodeValueDictionary);
    }

    public Type EvaluateType(Dictionary<string, object?>? variables = null)
    {
        return base.EvaluateType(_postfixTokens, variables, _stack, _nodeDictionary, _nodeValueDictionary);
    }

    public EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments() =>
        base.CheckEmptyFunctionArguments(_nodeDictionary);




    #endregion


}
