using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.UnaryOperators;

public sealed class UnaryOperatorSyntaxDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Scenario { get; init; }

    // Stringified type names
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? OperandTypes { get; init; }

    // Multiple examples at syntax level
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Examples { get; init; }

    // Must be last in JSON
    [JsonPropertyOrder(int.MaxValue)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
