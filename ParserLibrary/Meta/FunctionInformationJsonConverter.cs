using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParserLibrary.Parsers;

namespace ParserLibrary.Meta;

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

        Write(writer, options, nameof(FunctionInformation.IsCustomFunction), value.IsCustomFunction);

        if (value.MinArgumentsCount.HasValue)
            Write(writer, options, nameof(FunctionInformation.MinArgumentsCount), value.MinArgumentsCount.Value);

        if (value.MaxArgumentsCount.HasValue)
            Write(writer, options, nameof(FunctionInformation.MaxArgumentsCount), value.MaxArgumentsCount.Value);

        if (value.FixedArgumentsCount.HasValue)
            Write(writer, options, nameof(FunctionInformation.FixedArgumentsCount), value.FixedArgumentsCount.Value);

        // Examples
        if (value.Examples is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.Examples));
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

        // Types (names instead of Type)
        if (value.AllowedTypesPerPosition is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedTypesPerPosition");
            writer.WriteStartArray();
            for (int idx = 0; idx < value.AllowedTypesPerPosition.Count; idx++)
            {
                var set = value.AllowedTypesPerPosition[idx];
                if (set is null || set.Count == 0) continue;

                var names = new HashSet<string>(set.Select(TypeNameDisplay.GetDisplayTypeName));
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
            WriteStringArray(writer, value.AllowedTypesForAll.Select(TypeNameDisplay.GetDisplayTypeName));
        }

        if (value.AllowedTypesForLast is { Count: > 0 })
        {
            WritePropName(writer, options, "AllowedTypesForLast");
            WriteStringArray(writer, value.AllowedTypesForLast.Select(TypeNameDisplay.GetDisplayTypeName));
        }

        // String values per position
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

        // String formats per position
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

        // Function syntaxes (static input/output typing)
        if (value.Syntaxes is { Count: > 0 })
        {
            WritePropName(writer, options, nameof(FunctionInformation.Syntaxes));
            writer.WriteStartArray();

            foreach (var syn in value.Syntaxes)
            {
                writer.WriteStartObject();

                if (syn.Scenario.HasValue)
                    Write(writer, options, nameof(FunctionSyntax.Scenario), syn.Scenario.Value);

                if (!string.IsNullOrWhiteSpace(syn.Expression))
                    Write(writer, options, nameof(FunctionSyntax.Expression), syn.Expression!);

                if (!string.IsNullOrWhiteSpace(syn.ExpressionClean))
                    Write(writer, options, nameof(FunctionSyntax.ExpressionClean), syn.ExpressionClean!);

                // InputsFixed: array of { position, types[] }
                if (syn.InputsFixed is { Count: > 0 })
                {
                    WritePropName(writer, options, nameof(FunctionSyntax.InputsFixed));
                    writer.WriteStartArray();
                    for (int i = 0; i < syn.InputsFixed.Count; i++)
                    {
                        var set = syn.InputsFixed[i];
                        if (set is null || set.Count == 0) continue;

                        writer.WriteStartObject();
                        Write(writer, options, "Position", i + 1); // 1-based
                        WritePropName(writer, options, "Types");
                        WriteStringArray(writer, set.Select(TypeNameDisplay.GetDisplayTypeName).Distinct());
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }

                // InputsDynamic: { firstInputTypes?, lastInputTypes?, types?, minVariableArgumentsCount? }
                if (syn.InputsDynamic.HasValue)
                {
                    var dyn = syn.InputsDynamic.Value;
                    var hasFirst = dyn.FirstInputType is { Count: > 0 };
                    var hasLast = dyn.LastInputType is { Count: > 0 };
                    var hasTypes = dyn.MiddleInputTypes is { Count: > 0 };
                    var minVar = dyn.MinVariableArgumentsCount;

                    if (hasFirst || hasLast || hasTypes || minVar > 0)
                    {
                        WritePropName(writer, options, nameof(FunctionSyntax.InputsDynamic));
                        writer.WriteStartObject();

                        if (hasFirst)
                        {
                            WritePropName(writer, options, "FirstInputTypes");
                            WriteStringArray(writer, dyn.FirstInputType!.Select(TypeNameDisplay.GetDisplayTypeName).Distinct());
                        }

                        if (hasLast)
                        {
                            WritePropName(writer, options, "LastInputTypes");
                            WriteStringArray(writer, dyn.LastInputType!.Select(TypeNameDisplay.GetDisplayTypeName).Distinct());
                        }

                        if (hasTypes)
                        {
                            WritePropName(writer, options, "Types");
                            WriteStringArray(writer, dyn.MiddleInputTypes!.Select(TypeNameDisplay.GetDisplayTypeName).Distinct());
                        }

                        if (minVar > 0)
                        {
                            WritePropName(writer, options, "MinVariableArgumentsCount");
                            writer.WriteNumberValue(minVar);
                        }

                        writer.WriteEndObject();
                    }
                }

                // Multiple syntax examples (array) BEFORE single Example/Description/OutputType
                if (syn.Examples is { Length: > 0 })
                {
                    WritePropName(writer, options, nameof(FunctionSyntax.Examples));
                    WriteStringArray(writer, syn.Examples.Distinct());
                }

                if (!string.IsNullOrWhiteSpace(syn.Description))
                    Write(writer, options, nameof(FunctionSyntax.Description), syn.Description!);

                // Output type LAST
                if (syn.OutputType is not null)
                {
                    WritePropName(writer, options, nameof(FunctionSyntax.OutputType));
                    writer.WriteStringValue(TypeNameDisplay.GetDisplayTypeName(syn.OutputType));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
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
    private static void Write(Utf8JsonWriter writer, JsonSerializerOptions options, string clrName, bool value)
    {
        WritePropName(writer, options, clrName);
        writer.WriteBooleanValue(value);
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