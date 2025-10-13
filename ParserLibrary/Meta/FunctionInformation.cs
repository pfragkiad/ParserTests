using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public readonly struct FunctionInformation
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public byte? MinArgumentsCount { get; init; }
    public byte? MaxArgumentsCount { get; init; }
    public byte? FixedArgumentsCount { get; init; }

    public IList<SyntaxExample>? Examples { get; init; }

    // Types: ignored in JSON to avoid reflection graphs and schema collisions
    [JsonIgnore] public IReadOnlyList<HashSet<Type>>? AllowedTypesPerPosition { get; init; }
    [JsonIgnore] public HashSet<Type>? AllowedTypesForAll { get; init; }
    [JsonIgnore] public HashSet<Type>? AllowedTypeForLast { get; init; }

    // String values (0-based keys internally)
    [JsonIgnore] public Dictionary<int, HashSet<string>>? AllowedStringValuesPerPosition { get; init; }
    public HashSet<string>? AllowedStringValuesForAll { get; init; }
    public HashSet<string>? AllowedStringValuesForLast { get; init; }

    [JsonIgnore] public Dictionary<int, HashSet<string>>? AllowedStringFormatsPerPosition { get; init; }
    public HashSet<string>? AllowedStringFormatsForAll { get; init; }
    public HashSet<string>? AllowedStringFormatsForLast { get; init; }
}
