using System.Linq;
using System.Text.Json.Serialization;
using ParserLibrary.Parsers;

namespace ParserLibrary.Meta;

public sealed class BinaryOperatorDefinitionDto
{
    public required string Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Aliases { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SyntaxExampleDto>? Examples { get; init; }

    // Operator syntaxes
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BinaryOperatorSyntaxDto>? Syntaxes { get; init; }

    // Factory mapper from BinaryOperatorInformation
    public static BinaryOperatorDefinitionDto From(BinaryOperatorInformation src)
    {
        return new BinaryOperatorDefinitionDto
        {
            Name = src.Name,
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

    private static BinaryOperatorSyntaxDto MapSyntax(BinaryOperatorSyntax syn)
    {
        return new BinaryOperatorSyntaxDto
        {
            Scenario = syn.Scenario,
            LeftTypes = syn.LeftTypes is { Count: > 0 }
                ? [.. syn.LeftTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()]
                : null,
            RightTypes = syn.RightTypes is { Count: > 0 }
                ? [.. syn.RightTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()]
                : null,
            // multi-examples array
            Examples = syn.Examples is { Length: > 0 } ? syn.Examples.Distinct().ToList() : null,
            // ensure output last via JsonPropertyOrder
            OutputType = syn.OutputType is not null
                ? TypeNameDisplay.GetDisplayTypeName(syn.OutputType)
                : null,
            Description = string.IsNullOrWhiteSpace(syn.Description) ? null : syn.Description
        };
    }
}

public sealed class BinaryOperatorSyntaxDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Scenario { get; init; }

    // Stringified type names
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LeftTypes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RightTypes { get; init; }

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

// Optional convenience extension
public static class BinaryOperatorDefinitionDtoExtensions
{
    public static BinaryOperatorDefinitionDto ToDefinitionDto(this BinaryOperatorInformation src)
        => BinaryOperatorDefinitionDto.From(src);
}