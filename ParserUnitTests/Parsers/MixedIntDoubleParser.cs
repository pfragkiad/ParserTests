using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests.Parsers;

public class MixedIntDoubleParser(
    ILogger<Parser> logger, IOptions<TokenizerOptions> options) : Parser(logger, options)
{
    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        return functionName switch
        {
            "sin" => (int)args[0]! * (double)args[1]!,
            _ => base.EvaluateFunction(functionName, args)
        };

    }

    protected override object EvaluateLiteral(string s)
    {
        if (int.TryParse(s, out int i))
            return i;

        return double.Parse(s, CultureInfo.InvariantCulture);
    }

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        if (leftOperand is null || rightOperand is null)
        {
            _logger.LogError("Invalid operands for operator {OperatorName}: {LeftOperand}, {RightOperand}", operatorName, leftOperand, rightOperand);
            throw new ArgumentException($"Invalid operands for operator {operatorName}");
        }

        dynamic l = leftOperand as dynamic;
        dynamic r = rightOperand as dynamic;

        return operatorName switch
        {

            "+" => l + r,
            _ => base.EvaluateOperator(operatorName, leftOperand, rightOperand)
        };
    }
   
}
