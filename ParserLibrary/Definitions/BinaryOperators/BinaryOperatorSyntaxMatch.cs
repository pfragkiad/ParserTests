namespace ParserLibrary.Definitions.BinaryOperators;

public sealed class BinaryOperatorSyntaxMatch
{
    public required BinaryOperatorSyntax MatchedSyntax { get; init; }
    public required Type LeftType { get; init; }
    public required Type RightType { get; init; }
}