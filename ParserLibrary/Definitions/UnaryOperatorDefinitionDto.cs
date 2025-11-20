using System.Linq;
using System.Text.Json.Serialization;
using ParserLibrary.Parsers;

namespace ParserLibrary.Definitions;

public sealed class UnaryOperatorDefinitionDto
{
    public required string Name { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UnaryOperatorKind Kind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Aliases { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SyntaxExampleDto>? Examples { get; init; }

    // Operator syntaxes
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UnaryOperatorSyntaxDto>? Syntaxes { get; init; }

    // Factory mapper from UnaryOperatorInformation
    public static UnaryOperatorDefinitionDto From(UnaryOperatorDefinition src)
    {
        return new UnaryOperatorDefinitionDto
        {
            Name = src.Name,
            Kind = src.Kind,
            Description = string.IsNullOrWhiteSpace(src.Description) ? null : src.Description,

            Aliases = src.Aliases is { Length: > 0 }
                ? [.. src.Aliases.Distinct()]
                : null,

            Examples = src.Examples is { Count: > 0 }
                ? [.. src.Examples.Select(e => new SyntaxExampleDto
                {
                    Syntax = e.Syntax,
                    Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description
                })]
                : null,

            Syntaxes = src.Syntaxes is { Count: > 0 }
                ? [.. src.Syntaxes.Select(MapSyntax)]
                : null
        };
    }

    private static UnaryOperatorSyntaxDto MapSyntax(UnaryOperatorSyntax syn)
    {
        return new UnaryOperatorSyntaxDto
        {
            Scenario = syn.Scenario,
            OperandTypes = syn.OperandTypes is { Count: > 0 }
                ? [.. syn.OperandTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()]
                : null,
            Examples = syn.Examples is { Length: > 0 } ? syn.Examples.Distinct().ToList() : null,
            // ensure output last via JsonPropertyOrder
            OutputType = syn.OutputType is not null
                ? TypeNameDisplay.GetDisplayTypeName(syn.OutputType)
                : null,
            Description = string.IsNullOrWhiteSpace(syn.Description) ? null : syn.Description
        };
    }
}

public sealed class UnaryOperatorSyntaxDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Scenario { get; init; }

    // Stringified type names
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? OperandTypes { get; init; }

    // Multiple examples at syntax level
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Examples { get; init; }

    // Must be last in JSON
    [JsonPropertyOrder(int.MaxValue)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public static class UnaryOperatorDefinitionDtoExtensions
{
    public static UnaryOperatorDefinitionDto ToDefinitionDto(this UnaryOperatorDefinition src)
        => UnaryOperatorDefinitionDto.From(src);
}