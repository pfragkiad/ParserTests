namespace ParserUnitTests.Parsers;

public class MixedIntDoubleParser : Parser
{
    public MixedIntDoubleParser(
        ILogger<Parser> logger, ITokenizer tokenizer,
        IOptions<TokenizerOptions> options) :
        base(logger, tokenizer, options)
    { }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text;

        switch (functionName)
        {
            case "sin":
                {
                    //var separatorNode = functionNode.Right;
                    ////get 2 operands first as integer, second as double
                    //int v1 = (int)nodeValueDictionary[(Node<Token>)separatorNode.Left];
                    //double v2 = (double)nodeValueDictionary[(Node<Token>)separatorNode.Right];
                    var a = functionNode.GetFunctionArguments(2, nodeValueDictionary);
                    return (int)a[0] * (double)a[1];
                }
            default: return null;
        }
    }

    protected override object EvaluateLiteral(string s)
    {
        if (int.TryParse(s, out int i))
            return i;

        return double.Parse(s, CultureInfo.InvariantCulture);
    }

    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string operatorName = operatorNode.Text;
        switch (operatorName)
        {
            case "+":
                {
                    //object v1 = nodeValueDictionary[(Node<Token>)operatorNode.Left];
                    //object v2 = nodeValueDictionary[(Node<Token>)operatorNode.Right];
                    var v = operatorNode.GetBinaryArguments(nodeValueDictionary);

                    if (v.LeftOperand is int && v.RightOperand is int) return (int)v.LeftOperand + (int)v.RightOperand;
                    if (v.LeftOperand is double && v.RightOperand is double) return (double)v.LeftOperand + (double)v.RightOperand;
                    break;
                }
        }
        return null;
    }
}
