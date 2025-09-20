using ParserLibrary.Parsers.Compilation;
using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class ParserBase
{
    // Compile from expression string, optimization-aware (variables/type maps optional)
    public ParserCompilationResult Compile(
        string expression,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
        => Compile(
            expression,
            ParserCompilationOptions.FromOptimizationMode(optimizationMode),
            optimizationMode,
            variables,
            variableTypes,
            functionReturnTypes,
            ambiguousFunctionReturnTypes);

    // Compile from expression string with explicit options (no optimization by default)
    public ParserCompilationResult Compile(string expression, ParserCompilationOptions? options = null)
    {
        options ??= ParserCompilationOptions.Full;

        List<Token> infixTokens;
        try
        {
            infixTokens = GetInfixTokens(expression);
        }
        catch (Exception ex)
        {
            throw ParserCompileException.InfixException(ex);
        }

        return Compile(infixTokens, options.Value, optimizationMode: ExpressionOptimizationMode.None);
    }

    // Compile from expression string with options and optimization
    public ParserCompilationResult Compile(
        string expression,
        ParserCompilationOptions options,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        List<Token> infixTokens;
        try
        {
            infixTokens = GetInfixTokens(expression);
        }
        catch (Exception ex)
        {
            throw ParserCompileException.InfixException(ex);
        }

        return Compile(infixTokens, options, optimizationMode, variables, variableTypes, functionReturnTypes, ambiguousFunctionReturnTypes);
    }

    // Compile from already prepared infix tokens (no optimization)
    public ParserCompilationResult Compile(List<Token> infixTokens, ParserCompilationOptions options)
        => Compile(infixTokens, options, optimizationMode: ExpressionOptimizationMode.None);

    // Compile from already prepared infix tokens with optimization
    public ParserCompilationResult Compile(
        List<Token> infixTokens,
        ParserCompilationOptions options,
        ExpressionOptimizationMode optimizationMode,
        Dictionary<string, object?>? variables = null,
        Dictionary<string, Type>? variableTypes = null,
        Dictionary<string, Type>? functionReturnTypes = null,
        Dictionary<string, Func<Type?[], Type?>>? ambiguousFunctionReturnTypes = null)
    {
        var resultInfix = infixTokens;
        List<Token>? postfix = null;
        TokenTree? tree = null;

        try
        {
            if (options.BuildPostfix || options.BuildTree)
            {
                postfix = GetPostfixTokens(resultInfix);
            }

            if (options.BuildTree)
            {
                tree = GetExpressionTree(postfix!);

                // Optimization (optional)
                if (optimizationMode != ExpressionOptimizationMode.None && tree is not null)
                {
                    switch (optimizationMode)
                    {
                        default:
                        case ExpressionOptimizationMode.ParserInference:
                        {
                            // Optimize using runtime inference (variables)
                            var optimized = GetOptimizedTree(tree, variables);
                            tree = optimized.Tree;
                            break;
                        }

                        case ExpressionOptimizationMode.StaticTypeMaps:
                        {
                            // Build variable types from variables if not provided
                            variableTypes ??= BuildVariableTypesFromVariables(variables);
                            var optimized = tree.OptimizeForDataTypes(
                                _options.TokenPatterns,
                                variableTypes,
                                functionReturnTypes,
                                ambiguousFunctionReturnTypes);
                            tree = optimized.Tree;
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (postfix is null && (options.BuildPostfix || options.BuildTree))
                throw ParserCompileException.PostfixException(ex);

            if (tree is null && options.BuildTree)
                throw ParserCompileException.TreeBuildException(ex);

            throw;
        }

        return new ParserCompilationResult
        {
            InfixTokens = resultInfix,
            PostfixTokens = postfix,
            Tree = tree
        };
    }

    private static Dictionary<string, Type>? BuildVariableTypesFromVariables(Dictionary<string, object?>? variables) =>
        (variables is null || variables.Count == 0)
            ? null
            : variables
                .Where(kv => kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.GetType());
}

