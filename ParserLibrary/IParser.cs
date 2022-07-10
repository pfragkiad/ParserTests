using ParserLibrary.ExpressionTree;

namespace ParserLibrary
{
    public interface IParser
    {
        V Evaluate<V>(List<Token> postfixTokens, Func<string, V> literalParser, Dictionary<string, V> variables = null, Dictionary<string, Func<V, V, V>> operators = null, Dictionary<string, Func<V, V>>? funcs1Arg = null, Dictionary<string, Func<V, V, V>>? funcs2Arg = null);
        V Evaluate<V>(string s, Func<string, V> literalParser = null, Dictionary<string, V> variables = null, Dictionary<string, Func<V, V, V>> operators = null, Dictionary<string, Func<V, V>>? funcs1Arg = null, Dictionary<string, Func<V, V, V>>? funcs2Arg = null);
        object EvaluateCustom(List<Token> postfixTokens, Dictionary<string, object> variables = null);
        object EvaluateCustom(string s, Dictionary<string, object> variables = null);
        Tree<Token> GetExpressionTree(List<Token> postfixTokens);
        Tree<Token> Parse(string s);
    }
}