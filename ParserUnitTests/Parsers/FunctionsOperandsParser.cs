using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserUnitTests.Parsers;

public class FunctionsOperandsParser : DefaultParser
{
    public FunctionsOperandsParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : base(logger, options)
    {
    }

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        double op = GetDoubleUnaryOperand(operand);

        return operatorName switch
        {
            "!" => op * 2, //prefix custom
            "*" => op * 3, //prefix custom
            "%" => op + 2, //postfix custom
            _ => base.EvaluateUnaryOperator(operatorName, operand)
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] a = GetDoubleFunctionArguments(args);
        return functionName.ToLower() switch
        {
            "add" => a[0] + 2 * a[1],
            "add3" => a[0] + 2 * a[1] + 3 * a[2],
            _ => base.EvaluateFunction(functionName, args)
        };
    }


}
