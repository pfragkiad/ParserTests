using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserLibrary.ExpressionTree;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLibrary;

public class DefaultParser : Parser
{
    public DefaultParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    {    }

    protected override object EvaluateLiteral(string s) => 
        double.Parse(s, CultureInfo.InvariantCulture);

    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double operand = (double)nodeValueDictionary[
            (_options.TokenPatterns.UnaryOperatorDictionary[operatorNode.Text].Prefix ?
            operatorNode.Right : operatorNode.Left) as Node<Token>];
        switch (operatorNode.Text)
        {
            case "-": return -operand;
            case "+": return operand;
            default: return base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary);
        }
    }

    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double left = (double)nodeValueDictionary[operatorNode.Left as Node<Token>];
        double right = (double)nodeValueDictionary[operatorNode.Right as Node<Token>];
        switch (operatorNode.Text)
        {
            case "+": return left + right;
            case "-": return left - right;
            case "*": return left * right;
            case "/": return left / right;
            case "^": return Math.Pow(left, right);
            default: return base.EvaluateOperator(operatorNode, nodeValueDictionary);
        }
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {

        switch (functionNode.Text)
        {
            case "abs":
                {
                    double right = (double)nodeValueDictionary[functionNode.Right as Node<Token>];
                    return Math.Abs(right);
                }
            case "pow":
                {
                    double left = (double)nodeValueDictionary[functionNode.Right.Left as Node<Token>];
                    double right = (double)nodeValueDictionary[functionNode.Right.Right as Node<Token>];
                    return Math.Pow(left, right);
                }
        }

        return base.EvaluateFunction(functionNode, nodeValueDictionary);
    }

}
