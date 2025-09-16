namespace ParserLibrary.Parsers;

public sealed class ParserCompilationResult
{
    public required List<Token> InfixTokens { get; init; }
    public List<Token>? PostfixTokens { get; init; }
    public TokenTree? Tree { get; init; }
}