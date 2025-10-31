using System.Linq;
using System.Text.Json.Serialization;
using ParserLibrary.Parsers;

namespace ParserLibrary.Meta;

// Root DTO replacing custom converter output
public sealed class FunctionDefinitionDto
{
    public required string Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsCustomFunction { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MinArgumentsCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MaxArgumentsCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? FixedArgumentsCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SyntaxExampleDto>? Examples { get; init; }

    // Types (stringified)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AllowedTypesPerPositionDto>? AllowedTypesPerPosition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedTypesForAll { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedTypesForLast { get; init; }

    // String values
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ValuesPerPositionDto>? AllowedStringValuesPerPosition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedStringValuesForAll { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedStringValuesForLast { get; init; }

    // String formats
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ValuesPerPositionDto>? AllowedStringFormatsPerPosition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedStringFormatsForAll { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowedStringFormatsForLast { get; init; }

    // Function syntaxes
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FunctionSyntaxDto>? Syntaxes { get; init; }

    // Factory mapper from FunctionInformation
    public static FunctionDefinitionDto From(FunctionInformation src)
    {
        return new FunctionDefinitionDto
        {
            Name = src.Name,
            IsCustomFunction = src.IsCustomFunction,
            Description = string.IsNullOrWhiteSpace(src.Description) ? null : src.Description,
            MinArgumentsCount = src.MinArgumentsCount,
            MaxArgumentsCount = src.MaxArgumentsCount,
            FixedArgumentsCount = src.FixedArgumentsCount,

            Examples = src.Examples is { Count: > 0 }
                ? [.. src.Examples.Select(e => new SyntaxExampleDto
                {
                    Syntax = e.Syntax,
                    Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description
                })]
                : null,

            // Types (names instead of Type)
            AllowedTypesPerPosition = src.AllowedTypesPerPosition is { Count: > 0 }
                ? [.. src.AllowedTypesPerPosition
                    .Select((set, idx) => set is { Count: > 0 }
                        ? new AllowedTypesPerPositionDto
                        {
                            Position = idx + 1, // 1-based
                            Types = set.Select(TypeNameDisplay.GetDisplayTypeName)
                                       .Distinct()
                                       .ToList()
                        }
                        : null)
                    .Where(x => x is not null)
                    .Select(x => x!)]
                : null,

            AllowedTypesForAll = src.AllowedTypesForAll is { Count: > 0 }
                ? [.. src.AllowedTypesForAll.Select(TypeNameDisplay.GetDisplayTypeName)]
                : null,

            AllowedTypesForLast = src.AllowedTypesForLast is { Count: > 0 }
                ? [.. src.AllowedTypesForLast.Select(TypeNameDisplay.GetDisplayTypeName)]
                : null,

            // String values per position
            AllowedStringValuesPerPosition = src.AllowedStringValuesPerPosition is { Count: > 0 }
                ? [.. src.AllowedStringValuesPerPosition
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value is { Count: > 0 }
                        ? new ValuesPerPositionDto
                        {
                            Position = kv.Key + 1, // 1-based
                            Values = kv.Value.ToList()
                        }
                        : null)
                    .Where(x => x is not null)
                    .Select(x => x!)]
                : null,

            AllowedStringValuesForAll = src.AllowedStringValuesForAll is { Count: > 0 }
                ? [.. src.AllowedStringValuesForAll]
                : null,

            AllowedStringValuesForLast = src.AllowedStringValuesForLast is { Count: > 0 }
                ? [.. src.AllowedStringValuesForLast]
                : null,

            // String formats per position
            AllowedStringFormatsPerPosition = src.AllowedStringFormatsPerPosition is { Count: > 0 }
                ? [.. src.AllowedStringFormatsPerPosition
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value is { Count: > 0 }
                        ? new ValuesPerPositionDto
                        {
                            Position = kv.Key + 1, // 1-based
                            Values = kv.Value.ToList()
                        }
                        : null)
                    .Where(x => x is not null)
                    .Select(x => x!)]
                : null,

            AllowedStringFormatsForAll = src.AllowedStringFormatsForAll is { Count: > 0 }
                ? [.. src.AllowedStringFormatsForAll]
                : null,

            AllowedStringFormatsForLast = src.AllowedStringFormatsForLast is { Count: > 0 }
                ? [.. src.AllowedStringFormatsForLast]
                : null,

            // Function syntaxes
            Syntaxes = src.Syntaxes is { Count: > 0 }
                ? [.. src.Syntaxes.Select(MapSyntax)]
                : null
        };
    }

    private static FunctionSyntaxDto MapSyntax(FunctionSyntax syn)
    {
        var dyn = syn.InputsDynamic;

        // Map to { position, type } with 1-based positions
        var inputsFixed = syn.InputsFixed is { Count: > 0 }
            ? syn.InputsFixed
                .Select((t, idx) => t is null
                    ? null
                    : new InputFixedDto
                    {
                        Position = idx + 1,
                        Type = TypeNameDisplay.GetDisplayTypeName(t)
                    })
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList()
            : null;

        InputsDynamicDto? inputsDynamic = null;
        if (dyn is not null)
        {
            var hasFirst = dyn.Value.FirstInputType is not null;
            var hasLast = dyn.Value.LastInputType is not null;
            var hasTypes = dyn.Value.MiddleInputTypes is { Count: > 0 };
            var minVar = dyn.Value.MinVariableArgumentsCount;

            if (hasFirst || hasLast || hasTypes || minVar > 0)
            {
                inputsDynamic = new InputsDynamicDto
                {
                    FirstInputType = hasFirst ? TypeNameDisplay.GetDisplayTypeName(dyn.Value.FirstInputType!) : null,
                    LastInputType = hasLast ? TypeNameDisplay.GetDisplayTypeName(dyn.Value.LastInputType!) : null,
                    Types = hasTypes
                        ? dyn.Value.MiddleInputTypes!.Select(TypeNameDisplay.GetDisplayTypeName).ToList()
                        : null,
                    MinVariableArgumentsCount = minVar > 0 ? minVar : null
                };
            }
        }

        return new FunctionSyntaxDto
        {
            Scenario = syn.Scenario,
            Expression = string.IsNullOrWhiteSpace(syn.Expression) ? null : syn.Expression,
            InputsFixed = inputsFixed,
            InputsDynamic = inputsDynamic,
            // ensure output last via JsonPropertyOrder
            OutputType = syn.OutputType is not null
                ? TypeNameDisplay.GetDisplayTypeName(syn.OutputType)
                : null,
            Example = string.IsNullOrWhiteSpace(syn.Example) ? null : syn.Example,
            Description = string.IsNullOrWhiteSpace(syn.Description) ? null : syn.Description
        };
    }
}

public sealed class SyntaxExampleDto
{
    public required string Syntax { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public sealed class AllowedTypesPerPositionDto
{
    public int Position { get; init; }
    public required List<string> Types { get; init; }
}

public sealed class ValuesPerPositionDto
{
    public int Position { get; init; }
    public required List<string> Values { get; init; }
}

public sealed class FunctionSyntaxDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Scenario { get; init; }

    // Optional: expression for custom functions
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expression { get; init; }

    // Array of { position, type }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InputFixedDto>? InputsFixed { get; init; }

    // Object with optional first/last/types/minVariableArgumentsCount
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputsDynamicDto? InputsDynamic { get; init; }

    // Must be last in JSON
    [JsonPropertyOrder(int.MaxValue)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public sealed class InputFixedDto
{
    public int Position { get; init; }
    public required string Type { get; init; }
}

public sealed class InputsDynamicDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstInputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastInputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Types { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MinVariableArgumentsCount { get; init; }
}

// Optional convenience extension
public static class FunctionDefinitionDtoExtensions
{
    public static FunctionDefinitionDto ToDefinitionDto(this FunctionInformation src)
        => FunctionDefinitionDto.From(src);
}