using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserUnitTests.Parsers;

public class FunctionsOperandsParser : DefaultParser
{
    public FunctionsOperandsParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    {
    }


    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double operand = GetDoubleUnaryOperand(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "!" => operand * 2, //prefix custom
            "*" => operand * 3, //prefix custom
            "%" => operand + 2, //postfix custom
            _ => base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary)
        };
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double[] a = GetDoubleFunctionArguments(functionNode, nodeValueDictionary);

        return functionNode.Text.ToLower() switch
        {
            "add" => a[0] + 2 * a[1],
            "add3" => a[0] + 2 * a[1] + 3 * a[2],
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
        };
    }
}
