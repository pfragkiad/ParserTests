namespace ParserLibrary.Definitions.Functions;

public sealed class ValuesPerPositionDto
{
    public int Position { get; init; }
    public required List<string> Values { get; init; }
}
