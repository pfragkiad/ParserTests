namespace ParserLibrary.Definitions.Functions.Contracts;

public sealed class AllowedTypesPerPositionDto
{
    public int Position { get; init; }
    public required List<string> Types { get; init; }
}
