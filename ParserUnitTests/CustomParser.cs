using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserUnitTests;

public class CustomParser : Parser
{
    public CustomParser(
        ILogger<Parser> logger, ITokenizer tokenizer,
        IOptions<TokenizerOptions> options) :
        base(logger, tokenizer, options)
    {
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text;

        switch (functionName)
        {
            case "sin":
                {
                    var separatorNode = functionNode.Right;
                    //get 2 operands first as integer, second as double
                    int v1 = (int)nodeValueDictionary[(Node<Token>)separatorNode.Left];
                    double v2 = (double)nodeValueDictionary[(Node<Token>)separatorNode.Right];
                    return v1 * v2;
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
                    object v1 = nodeValueDictionary[(Node<Token>)operatorNode.Left];
                    object v2 = nodeValueDictionary[(Node<Token>)operatorNode.Right];
                    
                    if (v1 is int && v2 is int) return (int)v1 + (int)v2;
                    if (v1 is double && v2 is double) return (double)v1 + (double)v2;
                    return null;
                }
            default: return null;
        }
    }
}