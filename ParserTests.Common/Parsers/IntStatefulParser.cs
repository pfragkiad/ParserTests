using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserTests.Common.Parsers;

public class IntStatefulParser(
    ILogger<StatefulParserBase> logger,
    IOptions<TokenizerOptions> options) : StatefulParserBase(logger, options)
{
    protected override object EvaluateLiteral(string s)
        => int.Parse(s);


    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        if (leftOperand is not int || rightOperand is not int)
        {
            _logger.LogError("Invalid operands for operator {OperatorName}: {LeftOperand}, {RightOperand}", operatorName, leftOperand, rightOperand);
            throw new ArgumentException($"Invalid operands for operator {operatorName}");
        }

        int left = (int)leftOperand, right = (int)rightOperand;
        return operatorName switch
        {
            "+" => left + right,
            "*" => left * right,
            "^" => (int)Math.Pow(left, right),
            _ => base.EvaluateOperator(operatorName, leftOperand, rightOperand)
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        if (args.Length == 0 || args[0] is not int)
        {
            _logger.LogError("Invalid arguments for function {FunctionName}: {Args}", functionName, args);
            throw new ArgumentException($"Invalid arguments for function {functionName}");
        }

        int arg = (int)args[0]!;
        return functionName.ToLower() switch
        {
            "tan" => 10 * arg,
            "sin" => 2 * arg,
            _ => base.EvaluateFunction(functionName, args)
        };
    }
}
