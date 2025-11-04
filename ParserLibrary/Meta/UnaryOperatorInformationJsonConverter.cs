using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

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

        // NEW: Aliases
        if (value.Aliases is { Length: > 0 })
        {
            WritePropName(writer, options, nameof(UnaryOperatorInformation.Aliases));
            writer.WriteStartArray();
            foreach (var a in value.Aliases.Distinct())
                writer.WriteStringValue(a);
            writer.WriteEndArray();
        }

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

        // Legacy flat operand types (back-compat if rich syntaxes not provided)
        if ((value.Syntaxes is null || value.Syntaxes.Count == 0) && value.AllowedOperandTypes is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(UnaryOperatorInformation.AllowedOperandTypes));
            writer.WriteStartArray();
            foreach (var t in value.AllowedOperandTypes)
                writer.WriteStringValue(TypeNameDisplay.GetDisplayTypeName(t));
            writer.WriteEndArray();
        }

        // Rich syntaxes with per-syntax examples
        if (value.Syntaxes is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(UnaryOperatorInformation.Syntaxes));
            writer.WriteStartArray();
            foreach (var syn in value.Syntaxes)
            {
                writer.WriteStartObject();

                if (syn.Scenario.HasValue)
                    Write(writer, options, nameof(UnaryOperatorSyntax.Scenario), syn.Scenario.Value);

                if (syn.OperandTypes is { Count: > 0 })
                {
                    WritePropName(writer, options, nameof(UnaryOperatorSyntax.OperandTypes));
                    writer.WriteStartArray();
                    foreach (var t in syn.OperandTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct())
                        writer.WriteStringValue(t);
                    writer.WriteEndArray();
                }

                // Multiple examples only
                if (syn.Examples is { Length: > 0 })
                {
                    WritePropName(writer, options, nameof(UnaryOperatorSyntax.Examples));
                    writer.WriteStartArray();
                    foreach (var s in syn.Examples.Distinct())
                        writer.WriteStringValue(s);
                    writer.WriteEndArray();
                }

                if (!string.IsNullOrWhiteSpace(syn.Description))
                    Write(writer, options, nameof(UnaryOperatorSyntax.Description), syn.Description!);

                // OutputType last
                WritePropName(writer, options, nameof(UnaryOperatorSyntax.OutputType));
                writer.WriteStringValue(TypeNameDisplay.GetDisplayTypeName(syn.OutputType));

                writer.WriteEndObject();
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
    private static void Write(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName, int value)
    {
        WritePropName(writer, options, clrName);
        writer.WriteNumberValue(value);
    }

    private static void WritePropName(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName)
    {
        var name = options.PropertyNamingPolicy?.ConvertName(clrName) ?? clrName;
        writer.WritePropertyName(name);
    }
}