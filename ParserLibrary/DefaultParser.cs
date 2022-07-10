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
    { }

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

    const double TORAD = Math.PI / 180.0;
    readonly HashSet<string> functionsWith2Arguments = new HashSet<string> { "pow" };

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double a1 = 0.0, a2 = 0.0; //up to 2 arguments
        string functionName = functionNode.Text.ToLower();
        if (!functionsWith2Arguments.Contains(functionName))
            a1 = (double)nodeValueDictionary[functionNode.Right as Node<Token>];
        else //functionsWith2Arguments
        {
            a1 = (double)nodeValueDictionary[functionNode.Right.Left as Node<Token>];
            a2 = (double)nodeValueDictionary[functionNode.Right.Right as Node<Token>];
        }

        switch (functionName)
        {
            case "abs": return Math.Abs(a1);
            case "sin": return Math.Sin(a1);
            case "sind": return Math.Sin(a1 * TORAD);
            case "cos": return Math.Cos(a1);
            case "cosd": return Math.Cos(a1 * TORAD);
            case "pow": return Math.Pow(a1, a2);
            case "sqr":
            case "sqrt": return Math.Sqrt(a1);
            case "tan": return Math.Tan(a1);
            case "tand":  return Math.Tan(a1 * TORAD);
        }

        return base.EvaluateFunction(functionNode, nodeValueDictionary);
    }

}
