using ParserLibrary.Parsers.Interfaces;

namespace ParserLibrary.Parsers;

public partial class ParserBase
{
    // Compile from expression string, optimization-aware
    public ParserCompilationResult Compile(string expression, ExpressionOptimizationMode optimizationMode)
        => Compile(expression, ParserCompilationOptions.FromOptimizationMode(optimizationMode));

    // Compile from expression string with explicit options
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
            throw new ParserCompileException(ParserValidationStage.InfixTokenization, "Could not tokenize (get infix tokens).", ex);
        }

        return Compile(infixTokens, options.Value);
    }

    // Compile from already prepared infix tokens
    public ParserCompilationResult Compile(List<Token> infixTokens, ParserCompilationOptions options)
    {
        var resultInfix = infixTokens;
        List<Token>? postfix = null;
        TokenTree? tree = null;

        try
        {
            if (options.BuildPostfix || options.BuildTree)
                postfix = GetPostfixTokens(resultInfix);

            if (options.BuildTree)
                tree = GetExpressionTree(postfix!);
        }
        catch (Exception ex)
        {
            if (postfix is null && (options.BuildPostfix || options.BuildTree))
                throw new ParserCompileException(ParserValidationStage.PostfixTokenization, "Could not convert to postfix tokens.", ex);

            if (tree is null && options.BuildTree)
                throw new ParserCompileException(ParserValidationStage.TreeBuild, "Could not build expression tree.", ex);

            throw;
        }

        return new ParserCompilationResult
        {
            InfixTokens = resultInfix,
            PostfixTokens = postfix,
            Tree = tree,
        };
    }
}

