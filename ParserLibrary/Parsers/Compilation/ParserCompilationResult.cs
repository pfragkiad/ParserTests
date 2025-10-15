namespace ParserLibrary.Parsers.Compilation;

public sealed class ParserCompilationResult
{
    // Optional, but helpful for diagnostics/round-tripping
    public string? Expression { get; init; }

    public required List<Token> InfixTokens { get; init; }
    public List<Token>? PostfixTokens { get; init; }
    
    public TokenTree? Tree { get; init; }

    public TreeOptimizerResult? OptimizerResult { get; set; }

    public bool IsOptimized => OptimizerResult is not null;

    // Convenience flags for consumers
    public bool HasInfix => InfixTokens is { Count: > 0 };
    public bool HasPostfix => PostfixTokens is { Count: > 0 };
    public bool HasTree => Tree is not null;

    public static ParserCompilationResult Empty => new()
    {
        Expression = null,
        InfixTokens = [],
        PostfixTokens = null,
        Tree = null,
        OptimizerResult = null
    };

}