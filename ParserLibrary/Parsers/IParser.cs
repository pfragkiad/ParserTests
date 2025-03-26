using ParserLibrary.ExpressionTree;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

public interface IParser
{
    V Evaluate<V>(
        string s,
        Func<string, V>? literalParser = null,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V, V, V>>? binaryOperators = null,
        Dictionary<string, Func<V, V>>? unaryOperators = null,

        Dictionary<string, Func<V, V>>? funcs1Arg = null,
        Dictionary<string, Func<V, V, V>>? funcs2Arg = null,
        Dictionary<string, Func<V, V, V, V>>? funcs3Arg = null
        );

    object Evaluate(string s, Dictionary<string, object>? variables = null);
    Tree<Token> GetExpressionTree(List<Token> postfixTokens);
    Tree<Token> GetExpressionTree(string s);
}