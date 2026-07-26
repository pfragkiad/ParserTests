namespace ParserLibrary.Definitions.Functions;

public sealed class AllowedTypesPerPositionDto
{
    public int Position { get; init; }
    public required List<string> Types { get; init; }
}
