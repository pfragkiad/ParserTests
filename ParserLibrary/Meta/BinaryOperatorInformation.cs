using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

[JsonConverter(typeof(BinaryOperatorInformationJsonConverter))]
public readonly struct BinaryOperatorInformation
{
    // Symbol of the operator, e.g., "+", "-", "*", "/"
    public string Name { get; init; }
    public string? Description { get; init; }

    public IList<SyntaxExample>? Examples { get; init; }

    // Allowed operand type pairs. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<(Type Left, Type Right)>? AllowedTypePairs { get; init; }
}