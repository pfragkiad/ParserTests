using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.UnaryOperators;

public sealed class UnaryOperatorDefinitionDto
{
    public required string Name { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UnaryOperatorKind Kind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Aliases { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SyntaxExample>? Examples { get; init; }

    // Operator syntaxes
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UnaryOperatorSyntaxDto>? Syntaxes { get; init; }

 
}
