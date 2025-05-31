using OneOf;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

public interface IParser
{
    bool AreParenthesesMatched(string expression);
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

    //OneOf<T1, T2> Evaluate<T1, T2>(
    //    string s,
    //    Dictionary<string, OneOf<T1, T2>> variables
    //);

    //OneOf<T1, T2, T3> Evaluate<T1, T2, T3>(
    //    string s,
    //    Dictionary<string, OneOf<T1, T2, T3>> variables
    //);

    //OneOf<T1, T2, T3, T4> Evaluate<T1, T2, T3, T4>(
    //    string s,
    //    Dictionary<string, OneOf<T1, T2, T3, T4>> variables
    //);

    //OneOf<T1, T2, T3, T4, T5> Evaluate<T1, T2, T3, T4, T5>(
    //    string s,
    //    Dictionary<string, OneOf<T1, T2, T3, T4, T5>> variables
    //);

    //OneOf<T1, T2, T3, T4, T5, T6> Evaluate<T1, T2, T3, T4, T5, T6>(
    //    string s,
    //    Dictionary<string, OneOf<T1, T2, T3, T4, T5, T6>> variables
    //);

    Tree<Token> GetExpressionTree(List<Token> postfixTokens);
    Tree<Token> GetExpressionTree(string s);
    void RegisterFunction(string definition);
}