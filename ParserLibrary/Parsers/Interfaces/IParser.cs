using ParserLibrary.Tokenizers.CheckResults;
using ParserLibrary.Tokenizers.Interfaces;
using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;

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

    TokenTree GetExpressionTree(List<Token> postfixTokens);

    TokenTree GetExpressionTree(string expression);

    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(string expression);

    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(string expression);

    InvalidOperatorsCheckResult CheckOperators(string expression);

    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(string expression);

    // Optimizer APIs (full signatures)
    TreeOptimizerResult GetOptimizedExpressionTreeResult(
        string expression,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    TreeOptimizerResult GetOptimizedExpressionTreeResult(
        List<Token> postfixTokens,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    TokenTree GetOptimizedExpressionTree(
        string expression,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    TokenTree GetOptimizedExpressionTree(
        List<Token> postfixTokens,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null);

    // Parser-driven optimizer (runtime inference)
    TreeOptimizerResult GetOptimizedExpressionUsingParser(string expression, Dictionary<string, object?>? variables = null);
    TreeOptimizerResult OptimizeTreeUsingInference(TokenTree tree, Dictionary<string, object?>? variables = null);
}