using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.Functions.Contracts;

public sealed class InputsDynamicDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? FirstInputTypes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LastInputTypes { get; init; }

    // Middle input types (applies to all middle positions)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Types { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MinVariableArgumentsCount { get; init; }
}
