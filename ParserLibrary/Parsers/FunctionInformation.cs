using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace ParserLibrary.Parsers;

public readonly struct FunctionInformation
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public byte? MinArgumentsCount { get; init; }
    public byte? MaxArgumentsCount { get; init; }
    public byte? FixedArgumentsCount { get; init; }

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

    // ------------------ Type name translation (externally configurable) ------------------

    // External overrides by Type (preferred)
    public static ConcurrentDictionary<Type, string> CustomTypeNameMap { get; } = new();

    // External overrides by simple type name (case-insensitive)
    public static ConcurrentDictionary<string, string> CustomTypeNameMapByName { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterTypeName(Type type, string displayName) => CustomTypeNameMap[type] = displayName;
    public static void RegisterTypeName(string simpleTypeName, string displayName) => CustomTypeNameMapByName[simpleTypeName] = displayName;

    // Default aliases
    private static readonly IReadOnlyDictionary<Type, string> AliasTypeNames = new Dictionary<Type, string>
    {
        [typeof(string)] = "string",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(short)] = "short",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(void)] = "void",
        [typeof(object)] = "object",
        [typeof(char)] = "char",
        [typeof(sbyte)] = "sbyte",
        [typeof(ushort)] = "ushort",
        [typeof(uint)] = "uint",
        [typeof(ulong)] = "ulong",
        [typeof(TimeSpan)] = "TimeSpan",
    };

    // Public API: reuse anywhere
    public static string GetDisplayTypeName(Type t)
    {
        // 1) Exact type override
        if (CustomTypeNameMap.TryGetValue(t, out var custom)) return custom;

        // Handle Nullable<T>
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        if (!ReferenceEquals(underlying, t) && CustomTypeNameMap.TryGetValue(underlying, out custom))
            return custom;

        // 2) Override by simple name
        var simple = TrimGenericArity(underlying.Name);
        if (CustomTypeNameMapByName.TryGetValue(simple, out custom))
            return custom;

        // 3) Built-in aliases
        if (AliasTypeNames.TryGetValue(underlying, out var alias))
            return alias;

        // 4) Fallback to simple name
        return simple;
    }

    private static string TrimGenericArity(string name)
    {
        var backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }
}

public readonly struct FunctionSyntaxExample
{
    public string Example { get; init; }
    public string? Description { get; init; }
}
