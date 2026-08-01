namespace ParserLibrary.Definitions.Functions.Contracts;

public sealed class InputFixedDto
{
    public int Position { get; init; }
    public required List<string> Types { get; init; }
}
