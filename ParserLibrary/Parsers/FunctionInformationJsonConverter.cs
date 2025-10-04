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

        // type projections
        var typePerPos = value.AllowedTypeNamesPerPositionIndexed;
        if (typePerPos is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedTypeNamesPerPositionIndexed));
            writer.WriteStartArray();
            foreach (var item in typePerPos)
            {
                writer.WriteStartObject();
                Write(writer, options, nameof(FunctionInformation.PositionTypeNames.Position), item.Position);
                WritePropName(writer, options, nameof(FunctionInformation.PositionTypeNames.Types));
                WriteStringArray(writer, item.Types);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedTypeNamesForAll is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedTypeNamesForAll));
            WriteStringArray(writer, value.AllowedTypeNamesForAll);
        }

        if (value.AllowedTypeNamesForLast is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedTypeNamesForLast));
            WriteStringArray(writer, value.AllowedTypeNamesForLast);
        }

        // string values per position
        var valuesPerPos = value.AllowedStringValuesPerPositionIndexed;
        if (valuesPerPos is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedStringValuesPerPositionIndexed));
            writer.WriteStartArray();
            foreach (var item in valuesPerPos)
            {
                writer.WriteStartObject();
                Write(writer, options, nameof(FunctionInformation.PositionStringValues.Position), item.Position);
                WritePropName(writer, options, nameof(FunctionInformation.PositionStringValues.Values));
                WriteStringArray(writer, item.Values);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedStringValuesForAll is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedStringValuesForAll));
            WriteStringArray(writer, value.AllowedStringValuesForAll);
        }

        if (value.AllowedStringValuesForLast is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedStringValuesForLast));
            WriteStringArray(writer, value.AllowedStringValuesForLast);
        }

        // string formats per position
        var formatsPerPos = value.AllowedStringFormatsPerPositionIndexed;
        if (formatsPerPos is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedStringFormatsPerPositionIndexed));
            writer.WriteStartArray();
            foreach (var item in formatsPerPos)
            {
                writer.WriteStartObject();
                Write(writer, options, nameof(FunctionInformation.PositionStringValues.Position), item.Position);
                WritePropName(writer, options, nameof(FunctionInformation.PositionStringValues.Values));
                WriteStringArray(writer, item.Values);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (value.AllowedStringFormatsForAll is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedStringFormatsForAll));
            WriteStringArray(writer, value.AllowedStringFormatsForAll);
        }

        if (value.AllowedStringFormatsForLast is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.AllowedStringFormatsForLast));
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