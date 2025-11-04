using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace ParserLibrary.Meta;

public sealed class BinaryOperatorInformationJsonConverter : JsonConverter<BinaryOperatorInformation>
{
    public override BinaryOperatorInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("Deserialization for BinaryOperatorInformation is not supported.");

    public override void Write(Utf8JsonWriter writer, BinaryOperatorInformation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        Write(writer, options, nameof(BinaryOperatorInformation.Name), value.Name);

        // NEW: Aliases
        if (value.Aliases is { Length: > 0 })
        {
            WritePropName(writer, options, nameof(BinaryOperatorInformation.Aliases));
            writer.WriteStartArray();
            foreach (var a in value.Aliases.Distinct())
                writer.WriteStringValue(a);
            writer.WriteEndArray();
        }

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

        // Legacy flat pairs (kept for back-compat if syntaxes not provided)
        if ((value.Syntaxes is null || value.Syntaxes.Count == 0) && value.AllowedTypePairs is { Count: > 0 })
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

        // New: rich syntaxes with per-syntax examples
        if (value.Syntaxes is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(BinaryOperatorInformation.Syntaxes));
            writer.WriteStartArray();
            foreach (var syn in value.Syntaxes)
            {
                writer.WriteStartObject();

                if (syn.Scenario.HasValue)
                    Write(writer, options, nameof(BinaryOperatorSyntax.Scenario), syn.Scenario.Value);

                // Left/Right types (display names)
                if (syn.LeftTypes is { Count: > 0 })
                {
                    WritePropName(writer, options, nameof(BinaryOperatorSyntax.LeftTypes));
                    writer.WriteStartArray();
                    foreach (var t in syn.LeftTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct())
                        writer.WriteStringValue(t);
                    writer.WriteEndArray();
                }

                if (syn.RightTypes is { Count: > 0 })
                {
                    WritePropName(writer, options, nameof(BinaryOperatorSyntax.RightTypes));
                    writer.WriteStartArray();
                    foreach (var t in syn.RightTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct())
                        writer.WriteStringValue(t);
                    writer.WriteEndArray();
                }

                // Multiple examples first
                if (syn.Examples is { Length: > 0 })
                {
                    WritePropName(writer, options, nameof(BinaryOperatorSyntax.Examples));
                    writer.WriteStartArray();
                    foreach (var s in syn.Examples.Distinct())
                        writer.WriteStringValue(s);
                    writer.WriteEndArray();
                }

                if (!string.IsNullOrWhiteSpace(syn.Description))
                    Write(writer, options, nameof(BinaryOperatorSyntax.Description), syn.Description!);

                // OutputType last
                WritePropName(writer, options, nameof(BinaryOperatorSyntax.OutputType));
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