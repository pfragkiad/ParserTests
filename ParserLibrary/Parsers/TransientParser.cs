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

    public void Reset()
    {
        nodeValueDictionary = [];
        nodeDictionary = [];
        stack = []; 
    }

    public override object? Evaluate(string s, Dictionary<string, object?>? variables = null)
    {
        Reset();

        var postfixTokens = GetPostfixTokens(s);
        return base.Evaluate(postfixTokens, variables,stack,nodeDictionary,nodeValueDictionary);
    }

    public override Type EvaluateType(string s, Dictionary<string, object?>? variables = null)
    {
        var postfixTokens = GetPostfixTokens(s);
        return base.EvaluateType(postfixTokens, variables, stack, nodeDictionary,nodeValueDictionary);
    }


    #endregion


}
