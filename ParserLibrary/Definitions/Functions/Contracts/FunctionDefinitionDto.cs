using System.Linq;
using System.Text.Json.Serialization;
using ParserLibrary.Parsers;

namespace ParserLibrary.Definitions.Functions.Contracts;

// Root DTO replacing custom converter output
public sealed class FunctionDefinitionDto
{
    public required string Name { get; init; }


    // NEW: aliases for function names
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Aliases { get; init; }

    // NOTE: the custom converter always writes this (even when false).
    // To keep output identical, do NOT suppress default(false).
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
    public List<SyntaxExample>? Examples { get; init; }

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

  
}
