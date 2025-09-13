using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.Interfaces;
using ParserLibrary.Parsers.Interfaces;

namespace ParserTests.Common.Parsers;

public class SimpleFunctionParser(
    ILogger<DoubleParser> logger,
    IOptions<TokenizerOptions> options,
    ITokenizerValidator tokenizerValidator,
    IParserValidator parserValidator)
    : DoubleParser(logger, options, tokenizerValidator, parserValidator)
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
