using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParserLibrary.Parsers;

public sealed class FunctionInformationJsonConverter : JsonConverter<FunctionInformation>
{
    public override FunctionInformation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("Deserialization for FunctionInformation is not supported.");

    public override void Write(Utf8JsonWriter writer, FunctionInformation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // required
        Write(writer, options, nameof(FunctionInformation.Name), value.Name);

        // optional scalars
        if (!string.IsNullOrEmpty(value.Description))
            Write(writer, options, nameof(FunctionInformation.Description), value.Description);

        if (value.MinArgumentsCount.HasValue)
            Write(writer, options, nameof(FunctionInformation.MinArgumentsCount), value.MinArgumentsCount.Value);

        if (value.MaxArgumentsCount.HasValue)
            Write(writer, options, nameof(FunctionInformation.MaxArgumentsCount), value.MaxArgumentsCount.Value);

        if (value.FixedArgumentsCount.HasValue)
            Write(writer, options, nameof(FunctionInformation.FixedArgumentsCount), value.FixedArgumentsCount.Value);

        // --- Add Examples if present and non-empty ---
        if (value.Examples is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.Examples));
            writer.WriteStartArray();
            foreach (var ex in value.Examples)
            {
                writer.WriteStartObject();
                Write(writer, options, nameof(FunctionSyntaxExample.Syntax), ex.Syntax);
                if (!string.IsNullOrEmpty(ex.Description))
                    Write(writer, options, nameof(FunctionSyntaxExample.Description), ex.Description);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        // -------- type projections (names instead of types) --------
        if (value.AllowedTypesPerPosition is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedTypesPerPosition");
            writer.WriteStartArray();
            for (int idx = 0; idx < value.AllowedTypesPerPosition.Count; idx++)
            {
                var set = value.AllowedTypesPerPosition[idx];
                if (set is null || set.Count == 0) continue;

                var names = new HashSet<string>(set.Select(FunctionInformation.GetDisplayTypeName));
                writer.WriteStartObject();
                Write(writer, options, "Position", idx + 1); // 1-based
                WritePropName(writer, options, "Types");
                WriteStringArray(writer, names);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedTypesForAll is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedTypesForAll");
            WriteStringArray(writer, value.AllowedTypesForAll.Select(FunctionInformation.GetDisplayTypeName));
        }

        if (value.AllowedTypeForLast is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedTypesForLast");
            WriteStringArray(writer, value.AllowedTypeForLast.Select(FunctionInformation.GetDisplayTypeName));
        }

        // -------- string values per position (1-based positions) --------
        if (value.AllowedStringValuesPerPosition is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedStringValuesPerPosition");
            writer.WriteStartArray();
            foreach (var kv in value.AllowedStringValuesPerPosition.OrderBy(k => k.Key))
            {
                if (kv.Value is null || kv.Value.Count == 0) continue;
                writer.WriteStartObject();
                Write(writer, options, "Position", kv.Key + 1); // 1-based
                WritePropName(writer, options, "Values");
                WriteStringArray(writer, kv.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedStringValuesForAll is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedStringValuesForAll");
            WriteStringArray(writer, value.AllowedStringValuesForAll);
        }

        if (value.AllowedStringValuesForLast is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedStringValuesForLast");
            WriteStringArray(writer, value.AllowedStringValuesForLast);
        }

        // -------- string formats per position (1-based positions) --------
        if (value.AllowedStringFormatsPerPosition is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedStringFormatsPerPosition");
            writer.WriteStartArray();
            foreach (var kv in value.AllowedStringFormatsPerPosition.OrderBy(k => k.Key))
            {
                if (kv.Value is null || kv.Value.Count == 0) continue;
                writer.WriteStartObject();
                Write(writer, options, "Position", kv.Key + 1); // 1-based
                WritePropName(writer, options, "Values");
                WriteStringArray(writer, kv.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedStringFormatsForAll is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedStringFormatsForAll");
            WriteStringArray(writer, value.AllowedStringFormatsForAll);
        }

        if (value.AllowedStringFormatsForLast is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedStringFormatsForLast");
            WriteStringArray(writer, value.AllowedStringFormatsForLast);
        }

        writer.WriteEndObject();
    }

    private static void WriteStringArray(Utf8JsonWriter writer, IEnumerable<string> values)
    {
        writer.WriteStartArray();
        foreach (var v in values)
            writer.WriteStringValue(v);
        writer.WriteEndArray();
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

    private static void Write(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName, byte value)
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