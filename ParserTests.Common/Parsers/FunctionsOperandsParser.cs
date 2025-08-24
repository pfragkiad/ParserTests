using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Tokenizers;

namespace ParserTests.Common.Parsers;

public class FunctionsOperandsParser(ILogger<ParserBase> logger, IOptions<TokenizerOptions> options) :
    DefaultParser(logger, options)
{
    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        double d = GetDoubleUnaryOperand(operand);

        return operatorName switch
        {
            "!" => d * 2, //prefix custom
            "*" => d * 3, //prefix custom
            "%" => d + 2, //postfix custom
            _ => base.EvaluateUnaryOperator(operatorName, operand)
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] d = GetDoubleFunctionArguments(args);
        return functionName.ToLower() switch
        {
            "add" => d[0] + 2 * d[1],
            "add3" => d[0] + 2 * d[1] + 3 * d[2],
            _ => base.EvaluateFunction(functionName, args)
        };
    }


}
