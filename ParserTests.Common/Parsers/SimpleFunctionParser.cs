using Microsoft.Extensions.Logging;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;

namespace ParserTests.Common.Parsers;

public class SimpleFunctionParser(ILogger<DoubleParser> logger, ParserServices ps) : DoubleParser(logger, ps)
{

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] d = GetDoubleFunctionArguments(args);

        return functionName switch
        {
            "add3" => d[0] + 2 * d[1] + 3 * d[2],
            //for all other functions use the base class stuff (DefaultParser)
            _ => base.EvaluateFunction(functionName, args)
        };
    }

}
