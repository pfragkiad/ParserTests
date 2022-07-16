
namespace ParserUnitTests.Parsers;


public class IntParser : Parser
{
    public IntParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    { }

    protected override object EvaluateLiteral(string s)
    {
        return int.Parse(s);
    }

    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //int left = (int)nodeValueDictionary[operatorNode.Left as Node<Token>];
        //int right = (int)nodeValueDictionary[operatorNode.Right as Node<Token>];

        var operands = operatorNode.GetBinaryArguments(nodeValueDictionary);
        int left = (int)operands.LeftOperand, right = (int)operands.RightOperand;
        switch (operatorNode.Text)
        {
            case "+": return left + right;
            case "*": return left * right;
            case "^": return (int)Math.Pow(left, right);
            default: throw new InvalidOperationException($"Unknown operator ({operatorNode.Text})!");
        }
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //int right = (int)nodeValueDictionary[functionNode.Right as Node<Token>];
        int arg = (int)functionNode.GetFunctionArgument(nodeValueDictionary);
        switch (functionNode.Text)
        {
            case "tan": return 10 * arg;
            case "sin": return 2 * arg;
            default: throw new InvalidOperationException($"Unknown function ({functionNode.Text})!");

        }
    }
}
