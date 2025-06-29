using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests.Parsers;

public class SimpleFunctionParser : DefaultParser
{
    public SimpleFunctionParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : base(logger, options)
    {
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] a = GetDoubleFunctionArguments(args);

        return functionName switch
        {
            "add3" => a[0] + 2 * a[1] + 3 * a[2],
            //for all other functions use the base class stuff (DefaultParser)
            _ => base.EvaluateFunction(functionName, args)
        };
    }

}
