using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public enum UnaryOperatorKind : byte
{
    Prefix = 1,
    Postfix = 2
}

[JsonConverter(typeof(UnaryOperatorInformationJsonConverter))]
public class UnaryOperatorInformation :OperatorInformation
{
    // Unary placement kind
    public UnaryOperatorKind Kind { get; init; }

    // Allowed single-operand types. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<Type>? AllowedOperandTypes { get; init; }
}