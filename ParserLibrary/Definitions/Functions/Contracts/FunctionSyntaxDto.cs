using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.Functions.Contracts;

public sealed class FunctionSyntaxDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Scenario { get; init; }

    // Optional: expression for custom functions
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expression { get; init; }

     [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpressionClean { get; init; }

   // Array of { position, types[] } (1-based)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InputFixedDto>? InputsFixed { get; init; }

    // Object with optional first/last/types/minVariableArgumentsCount
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputsDynamicDto? InputsDynamic { get; init; }

    // Multiple examples at syntax level
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Examples { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasValueDependentOutputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? PossibleOutputTypes { get; init; }

    // Must be last in JSON
    [JsonPropertyOrder(int.MaxValue)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
