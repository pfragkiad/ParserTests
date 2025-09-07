using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;

namespace ParserLibrary.Parsers.Interfaces;

public interface IParser : ITokenizer
{
    FunctionNamesCheckResult CheckFunctionNames(string expression);
    List<string> GetMatchedFunctionNames(string expression);
    void RegisterFunction(string definition);


    V? Evaluate<V>(
        string expression,
        Func<string, V>? literalParser = null,
        Dictionary<string, V>? variables = null,
        Dictionary<string, Func<V?, V?, V?>>? binaryOperators = null,
        Dictionary<string, Func<V?, V?>>? unaryOperators = null,

        Dictionary<string, Func<V?, V?>>? funcs1Arg = null,
        Dictionary<string, Func<V?, V?, V?>>? funcs2Arg = null,
        Dictionary<string, Func<V?, V?, V?, V?>>? funcs3Arg = null
    );

    object? Evaluate(string expression, Dictionary<string, object?>? variables = null);

    Type EvaluateType(string expression, Dictionary<string, object?>? variables = null);

    object? EvaluateWithTreeOptimizer(string expression, Dictionary<string, object?>? variables = null);

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

    TokenTree GetExpressionTree(List<Token> postfixTokens);

    TokenTree GetExpressionTree(string expression);

    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression);

    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(string expression);

    InvalidOperatorsCheckResult CheckOperators(string expression);

    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression);
    TokenTree GetOptimizedExpressionTree(string expression, Dictionary<string, Type> variableTypes);
    TokenTree GetOptimizedExpressionTree(List<Token> postfixTokens, Dictionary<string, Type> variableTypes);
    TreeOptimizerResult GetOptimizedExpressionTreeResult(List<Token> postfixTokens, Dictionary<string, Type>? variableTypes = null);
    TreeOptimizerResult GetOptimizedExpressionTreeResult(string expression, Dictionary<string, Type>? variableTypes = null);
    TreeOptimizerResult GetOptimizedExpressionUsingParser(string expression, Dictionary<string, object?>? variables = null);
}