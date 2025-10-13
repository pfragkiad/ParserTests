using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public enum UnaryOperatorKind : byte
{
    Prefix = 1,
    Postfix = 2
}

[JsonConverter(typeof(UnaryOperatorInformationJsonConverter))]
public readonly struct UnaryOperatorInformation
{
    // Symbol of the operator, e.g., "+", "-", "!", "++"
    public string Name { get; init; }
    public string? Description { get; init; }

    public IList<SyntaxExample>? Examples { get; init; }

    // Unary placement kind
    public UnaryOperatorKind Kind { get; init; }

    // Allowed single-operand types. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<Type>? AllowedOperandTypes { get; init; }
}