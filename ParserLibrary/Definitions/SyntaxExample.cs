using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions;

public readonly struct SyntaxExample
{
    public required string Syntax { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

    public string? Description { get; init; }
}
