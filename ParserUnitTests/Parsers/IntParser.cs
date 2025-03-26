
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests.Parsers;


public class IntParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : Parser(logger, tokenizer, options)
{
    protected override object EvaluateLiteral(string s)
        => int.Parse(s);


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //int left = (int)nodeValueDictionary[operatorNode.Left as Node<Token>];
        //int right = (int)nodeValueDictionary[operatorNode.Right as Node<Token>];

        var operands = operatorNode.GetBinaryArguments(nodeValueDictionary);
        int left = (int)operands.LeftOperand, right = (int)operands.RightOperand;

        return operatorNode.Text switch
        {
            "+" => left + right,
            "*" => left * right,
            "^" => (int)Math.Pow(left, right),
            _ => base.EvaluateOperator(operatorNode, nodeValueDictionary)
        };
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //int right = (int)nodeValueDictionary[functionNode.Right as Node<Token>];
        int arg = (int)functionNode.GetFunctionArgument(nodeValueDictionary);

        return functionNode.Text.ToLower() switch
        {
            "tan" => 10 * arg,
            "sin" => 2 * arg,
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
        };
    }
}

public class IntTransientParser : TransientParser
{
    public IntTransientParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    { }

    protected override object EvaluateLiteral(string s)
        => int.Parse(s);


    protected override object EvaluateOperator(Node<Token> operatorNode)
    {
        var operands = GetBinaryArguments(operatorNode);
        int left = (int)operands.LeftOperand, right = (int)operands.RightOperand;

        return operatorNode.Text switch
        {
            "+" => left + right,
            "*" => left * right,
            "^" => (int)Math.Pow(left, right),
            _ => base.EvaluateOperator(operatorNode)
        };
    }

    protected override object EvaluateFunction(Node<Token> functionNode)
    {
        int arg = (int)GetFunctionArgument(functionNode);

        return functionNode.Text.ToLower() switch
        {
            "tan" => 10 * arg,
            "sin" => 2 * arg,
            _ => base.EvaluateFunction(functionNode)
        };
    }
}
