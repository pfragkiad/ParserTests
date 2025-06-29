using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

/// <summary>
/// This class can be use for a single evaluation, not for parallel evaluations, because the nodeValueDictionary and stack fields keep the state of the currently evaluated expression.
/// </summary>
public class TransientParser : Parser, ITransientParser
{

    //created for simplifying and caching dictionaries
    protected internal Dictionary<Node<Token>, object?> nodeValueDictionary = [];
    protected Dictionary<Token, Node<Token>> nodeDictionary = [];
    protected Stack<Token> stack = new();


    public TransientParser(ILogger<TransientParser> logger, IOptions<TokenizerOptions> options) 
        :base(logger, options)
    {  }

    protected TransientParser(ILogger logger, IOptions<TokenizerOptions> options)
    : base(logger, options)
    { }

    #region 

    public object? Evaluate(string s, Dictionary<string, object?>? variables = null)
    {
        //these properties are reset for each evaluation!
        nodeValueDictionary = [];
        nodeDictionary = [];
        stack = new Stack<Token>();

        var inOrderTokens = GetInOrderTokens(s);
        var postfixTokens = GetPostfixTokens(inOrderTokens);

        return Evaluate(postfixTokens, variables);
    }

    public Type EvaluateType(string s, Dictionary<string, object?>? variables = null)
    {
        var inOrderTokens = GetInOrderTokens(s);
        var postfixTokens = GetPostfixTokens(inOrderTokens);

        return EvaluateType(postfixTokens, variables);
    }

    protected override object? Evaluate(List<Token> postfixTokens, Dictionary<string, object?>? variables = null) =>
        base.Evaluate(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary);

    protected override Type EvaluateType(List<Token> postfixTokens, Dictionary<string, object?>? variables = null) =>
        base.EvaluateType(postfixTokens, variables, stack, nodeDictionary, nodeValueDictionary);
  


    #endregion


}
