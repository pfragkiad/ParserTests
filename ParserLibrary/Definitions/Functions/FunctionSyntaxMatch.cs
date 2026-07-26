namespace ParserLibrary.Definitions.Functions;

public class FunctionSyntaxMatch
{
    public required FunctionSyntax MatchedSyntax { get; init; }

    public Type[] ResolvedTypes { get; init; } = [];
}
