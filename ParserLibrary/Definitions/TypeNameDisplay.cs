using System.Collections.Concurrent;

namespace ParserLibrary.Definitions;

public static class TypeNameDisplay
{
    public static ConcurrentDictionary<Type, string> CustomTypeNameMap { get; } = new();
    public static ConcurrentDictionary<string, string> CustomTypeNameMapByName { get; }
        = new(StringComparer.OrdinalIgnoreCase);

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

    public static void RegisterTypeName(Type type, string displayName) => CustomTypeNameMap[type] = displayName;
    public static void RegisterTypeName(string simpleTypeName, string displayName) => CustomTypeNameMapByName[simpleTypeName] = displayName;

    public static string GetDisplayTypeName(Type t)
    {
        if (CustomTypeNameMap.TryGetValue(t, out var custom)) return custom;

        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        if (!ReferenceEquals(underlying, t) && CustomTypeNameMap.TryGetValue(underlying, out custom))
            return custom;

        var simple = TrimGenericArity(underlying.Name);
        if (CustomTypeNameMapByName.TryGetValue(simple, out custom))
            return custom;

        if (AliasTypeNames.TryGetValue(underlying, out var alias))
            return alias;

        return simple;
    }

    private static string TrimGenericArity(string name)
    {
        var backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }
}