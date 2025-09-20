namespace ParserLibrary.Parsers.Compilation;

public sealed class ParserCompilationResult
{
    public required List<Token> InfixTokens { get; init; }
    public List<Token>? PostfixTokens { get; init; }
    
    public TokenTree? Tree { get; init; }

    public TreeOptimizerResult? OptimizerResult { get; set; }

    public bool IsOptimized => OptimizerResult is not null;

    public static ParserCompilationResult Empty => new()
    {
        InfixTokens = [],
        PostfixTokens = null,
        Tree = null,
        OptimizerResult = null
    };

}