namespace ParserLibrary.Definitions.UnaryOperators;

public sealed class UnaryOperatorSyntaxMatch
{
    public required UnaryOperatorSyntax MatchedSyntax { get; init; }
    public required Type OperandType { get; init; }
}