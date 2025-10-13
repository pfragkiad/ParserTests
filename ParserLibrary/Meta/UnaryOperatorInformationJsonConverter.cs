using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public sealed class UnaryOperatorInformationJsonConverter : JsonConverter<UnaryOperatorInformation>
{
    public override UnaryOperatorInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("Deserialization for UnaryOperatorInformation is not supported.");

    public override void Write(Utf8JsonWriter writer, UnaryOperatorInformation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        Write(writer, options, nameof(UnaryOperatorInformation.Name), value.Name);

        if (!string.IsNullOrEmpty(value.Description))
            Write(writer, options, nameof(UnaryOperatorInformation.Description), value.Description);

        Write(writer, options, nameof(UnaryOperatorInformation.Kind), value.Kind.ToString());

        if (value.Examples is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(UnaryOperatorInformation.Examples));
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

        if (value.AllowedOperandTypes is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(UnaryOperatorInformation.AllowedOperandTypes));
            writer.WriteStartArray();
            foreach (var t in value.AllowedOperandTypes)
                writer.WriteStringValue(TypeNameDisplay.GetDisplayTypeName(t));
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