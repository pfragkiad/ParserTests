using System.Reflection;

namespace ParserLibrary.Meta;

public abstract class FunctionCatalog : MetadataCatalogBase<FunctionInformation>
{
    // Example property pattern:
    /*
    private FunctionInformation? _abs;
    public FunctionInformation Abs => (_abs ??= new()
    {
        Name = "abs",
        Description = "Returns the absolute value of a number or each value in a time series.",
        FixedArgumentsCount = 1,
        AllowedTypesPerPosition =
        [
            [
                typeof(SimpleData),
                typeof(double),
                typeof(int),
                typeof(TimeSpanWithMonthYears)
            ]
        ]
    }).Value;
    */

    public List<FunctionInformation> GetAllFunctions() => GetAllCore();

    public List<FunctionInformation> SearchFunctions(string searchTerm, StringComparison comparisonType)
        => SearchByNameOrDescription(searchTerm, comparisonType);

    //// for legacy argument count lookup
    //public Dictionary<string, byte> GetFunctionsWithFixedArgumentCount(StringComparer? comparer = null)
    //{
    //    comparer ??= StringComparer.OrdinalIgnoreCase;
    //    return GetAllCore()
    //        .Where(fi => fi.FixedArgumentsCount.HasValue)
    //        .ToDictionary(fi => fi.Name, fi => fi.FixedArgumentsCount!.Value, comparer);
    //}

    //public Dictionary<string, (byte min, byte max)> GetFunctionsWithVariableArgumentCount(StringComparer? comparer = null)
    //{
    //    comparer ??= StringComparer.OrdinalIgnoreCase;
    //    return GetAllCore()
    //        .Where(fi => fi.MinArgumentsCount.HasValue || fi.MaxArgumentsCount.HasValue)
    //        .ToDictionary(fi => fi.Name, fi => (fi.MinArgumentsCount!.Value, fi.MaxArgumentsCount!.Value), comparer);
    //}

    // Convenience helper for implementers
    protected FunctionInformation? FindFunctionInformation(string functionName, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            return null;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var fi in GetAllCore())
        {
            if (fi.Name.Equals(functionName, comparison))
                return fi;
        }
        return null;
    }

    public abstract FunctionInformation? GetFunctionInformation(string functionName, bool caseSensitive);
}
