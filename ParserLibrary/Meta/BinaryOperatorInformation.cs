using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

[JsonConverter(typeof(BinaryOperatorInformationJsonConverter))]
public class BinaryOperatorInformation : OperatorInformation
{
    // Allowed operand type pairs. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<(Type Left, Type Right)>? AllowedTypePairs { get; init; }
}