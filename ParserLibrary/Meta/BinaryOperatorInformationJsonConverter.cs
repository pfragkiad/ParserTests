using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public sealed class BinaryOperatorInformationJsonConverter : JsonConverter<BinaryOperatorInformation>
{
    public override BinaryOperatorInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("Deserialization for BinaryOperatorInformation is not supported.");

    public override void Write(Utf8JsonWriter writer, BinaryOperatorInformation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        Write(writer, options, nameof(BinaryOperatorInformation.Name), value.Name);

        if (!string.IsNullOrEmpty(value.Description))
            Write(writer, options, nameof(BinaryOperatorInformation.Description), value.Description);

        if (value.Examples is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(BinaryOperatorInformation.Examples));
            writer.WriteStartArray();
            foreach (var ex in value.Examples)
            {
                writer.WriteStartObject();
                Write(writer, options, nameof(SyntaxExample.Syntax), ex.Syntax);
                if (!string.IsNullOrEmpty(ex.Description))
                    Write(writer, options, nameof(SyntaxExample.Description), ex.Description);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedTypePairs is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(BinaryOperatorInformation.AllowedTypePairs));
            writer.WriteStartArray();
            foreach (var (left, right) in value.AllowedTypePairs)
            {
                writer.WriteStartArray();
                writer.WriteStringValue(TypeNameDisplay.GetDisplayTypeName(left));
                writer.WriteStringValue(TypeNameDisplay.GetDisplayTypeName(right));
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void Write(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName, string value)
    {
        WritePropName(writer, options, clrName);
        writer.WriteStringValue(value);
    }

    private static void WritePropName(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName)
    {
        var name = options.PropertyNamingPolicy?.ConvertName(clrName) ?? clrName;
        writer.WritePropertyName(name);
    }
}