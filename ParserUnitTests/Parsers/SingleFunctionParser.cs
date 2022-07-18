using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserUnitTests.Parsers;

public class SimpleFunctionParser : DefaultParser
{
    public SimpleFunctionParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    {
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double[] a = GetDoubleFunctionArguments( functionNode, nodeValueDictionary);

        return functionNode.Text.ToLower() switch
        {
            "add3" => a[0] + 2 * a[1] + 3 * a[2],
            //for all other functions use the base class stuff (DefaultParser)
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
        };
    }
}
