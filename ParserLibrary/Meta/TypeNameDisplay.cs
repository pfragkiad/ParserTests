using System.Collections.Concurrent;

namespace ParserLibrary.Meta;

// DI-friendly concrete service (no interface).
// You can register as Scoped or Singleton. Prefer Singleton unless you truly need per-scope overrides.
public sealed class TypeNameDisplay
{
    // Optional shared default for places without DI (static callers, tests, tools, etc.)
    public static TypeNameDisplay Shared { get; } = new();

    // Instance state (per registration/scope)
    public ConcurrentDictionary<Type, string> CustomTypeNameMap { get; } = new();
    public ConcurrentDictionary<string, string> CustomTypeNameMapByName { get; }
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

    // Instance APIs
    public void RegisterTypeName(Type type, string displayName) => CustomTypeNameMap[type] = displayName;
    public void RegisterTypeName(string simpleTypeName, string displayName) => CustomTypeNameMapByName[simpleTypeName] = displayName;

    public string GetDisplayTypeName(Type t)
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

    // Static convenience wrappers (redirect to Shared). Avoid these if you require scoped behavior.
    public static void RegisterTypeNameStatic(Type type, string displayName) => Shared.RegisterTypeName(type, displayName);
    public static void RegisterTypeNameStatic(string simpleTypeName, string displayName) => Shared.RegisterTypeName(simpleTypeName, displayName);
    public static string GetDisplayTypeNameStatic(Type t) => Shared.GetDisplayTypeName(t);
}