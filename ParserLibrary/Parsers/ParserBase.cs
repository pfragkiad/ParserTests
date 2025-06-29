using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers;

public class ParserBase  : Tokenizer, IParserBase
{
    public ParserBase(ILogger logger, IOptions<TokenizerOptions> options)
        :base(logger, options)
    {  }

    protected Dictionary<string, (string[] Parameters, string Body)> _customFunctions = [];

    //needed for checking the function identifiers and arguments count in the expression tree
    protected virtual Dictionary<string,int> MainFunctions => [];

    public void RegisterFunction(string definition)
    {
        // Example: "myf(x,y) = 10*x+sin(y)"
        var parts = definition.Split('=', 2);
        if (parts.Length != 2)
            throw new ArgumentException("Invalid function definition format.");

        var header = parts[0].Trim();
        var body = parts[1].Trim();

        var nameAndParams = header.Split('(', 2);
        if (nameAndParams.Length != 2 || !nameAndParams[1].EndsWith(")"))
            throw new ArgumentException("Invalid function header format.");

        var name = nameAndParams[0].Trim();

        var paramList = nameAndParams[1][..^1].Split(_options.TokenPatterns.ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _customFunctions[name] = (paramList, body);
    }


    public FunctionNamesCheckResult CheckFunctionNames(string expression)
    {
        //returns the names of the functions that are not registered
        var tokens = GetInOrderTokens(expression);
        //var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);

        HashSet<string> matchedNames = [];
        HashSet<string> unmatchedNames = [];
        foreach (var t in tokens.Where(t => t.TokenType == TokenType.Function))
        {
            if (_customFunctions.ContainsKey(t.Text) || MainFunctions.ContainsKey(t.Text))
            { matchedNames.Add(t.Text); continue; }

            unmatchedNames.Add(t.Text);
        }

        return new FunctionNamesCheckResult
        {
            MatchedNames = [.. matchedNames],
            UnmatchedNames = [.. unmatchedNames]
        };
    }

    public List<string> GetMatchedFunctionNames(string expression)
    {
        //returns the names of the functions that are registered
        var tokens = GetInOrderTokens(expression);
        //var postfixTokens = _tokenizer.GetPostfixTokens(inOrderTokens);
        return [.. tokens
            .Where(t => t.TokenType == TokenType.Function &&
                (_customFunctions.ContainsKey(t.Text) || MainFunctions.ContainsKey(t.Text)))
            .Select(t => t.Text)
            .Distinct()];
    }


    #region Overrides for all parsers

    protected virtual object? EvaluateFunction(string functionName, object?[] args)
    {
        throw new InvalidOperationException($"Unknown function ({functionName})");
    }

    protected virtual Type EvaluateFunctionType(string functionName, object?[] args)
    {
        throw new InvalidOperationException($"Unknown function ({functionName})");
    }

    protected virtual object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        throw new InvalidOperationException($"Unknown operator ({operatorName})");
    }

    protected virtual Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand)
    {
        throw new InvalidOperationException($"Unknown operator ({operatorName})");
    }

    protected virtual object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");
    }

    protected virtual Type EvaluateUnaryOperatorType(string operatorName, object? operand)
    {
        throw new InvalidOperationException($"Unknown unary operator ({operatorName})");
    }

    protected virtual object? EvaluateLiteral(string s)
    {
        return new();
    }






    #endregion

    #region Get arguments

    protected static object? GetUnaryArgument(bool isPrefix, Node<Token> unaryOperatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary) =>
    unaryOperatorNode.GetUnaryArgument(isPrefix, nodeValueDictionary);

    protected static (object? LeftOperand, object? RightOperand) GetBinaryArguments(Node<Token> binaryOperatorNode, Dictionary<Node<Token>, object?> nodeValueDictionary) =>
        binaryOperatorNode.GetBinaryArguments(nodeValueDictionary);

    protected static object? GetFunctionArgument(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary) =>
        functionNode.GetFunctionArgument(nodeValueDictionary);

    protected object?[] GetFunctionArguments(Node<Token> functionNode, Dictionary<Node<Token>, object?> nodeValueDictionary) =>
        functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, nodeValueDictionary);

    protected Node<Token>[] GetFunctionArgumentNodes(Node<Token> functionNode) =>
        functionNode.GetFunctionArgumentNodes(_options.TokenPatterns.ArgumentSeparator);



    #endregion
}
