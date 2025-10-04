using System.Reflection;

namespace ParserLibrary.Parsers;


public abstract class ParserFunctionsMetaBase
{
    //The child classes should implement properties like this:

    /*
 private FunctionInformation? _absFunctionInfo;
    public FunctionInformation AbsFunctionInfo => (_absFunctionInfo ??= _absFunctionInfo = new()
    {
        Name = "abs",
        Description = "Returns the absolute value of a number or each value in a time series.",
        FixedArgumentsCount = 1,
        AllowedTypesPerPosition =
    [
           [ //position 0
                typeof(SimpleData),
                typeof(double),
                typeof(int),
                typeof(TimeSpanWithMonthYears)
           ]
    ]
    }).Value;
     */

    protected List<FunctionInformation>? _allFunctionInformationsCache;
    public List<FunctionInformation> GetAllFunctions()
    {
        if (_allFunctionInformationsCache is not null)
            return _allFunctionInformationsCache;

        var props = this.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(FunctionInformation));

        _allFunctionInformationsCache = [.. props
            .Select(p => (FunctionInformation)p.GetValue(this)!)
            .OrderBy(fi => fi.Name, StringComparer.OrdinalIgnoreCase)];

        return _allFunctionInformationsCache;
    }


    public List<FunctionInformation> SearchFunctions(string searchTerm, StringComparison comparisonType)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetAllFunctions();
        searchTerm = searchTerm.Trim();

        var allFunctions = GetAllFunctions();
        // Simple case-insensitive substring match in name or description
        var results = allFunctions
            .Where(fi => fi.Name.Contains(searchTerm, comparisonType) ||
                         (fi.Description?.Contains(searchTerm, comparisonType) ?? false))
            .ToList();
        return results;
    }


    //for legacy argument count lookup
    public Dictionary<string, byte> GetFunctionsWithFixedArgumentCount(StringComparer? comparer = null)
    {
        comparer ??= StringComparer.OrdinalIgnoreCase;
        return GetAllFunctions()
            .Where(fi => fi.FixedArgumentsCount.HasValue)
            .ToDictionary(fi => fi.Name,
            fi => fi.FixedArgumentsCount!.Value, comparer);
    }

    public Dictionary<string, (byte min, byte max)> GetFunctionsWithVariableArgumentCount(StringComparer? comparer = null)
    {
        comparer ??= StringComparer.OrdinalIgnoreCase;
        return GetAllFunctions()
            .Where(fi => fi.MinArgumentsCount.HasValue || fi.MaxArgumentsCount.HasValue)
            .ToDictionary(fi => fi.Name,
            fi => (fi.MinArgumentsCount!.Value, fi.MaxArgumentsCount!.Value), comparer);
    }

    //Example:
    //string f = _options.CaseSensitive ? functionName : functionName.ToLower();
    //return f switch
    //{
    //    //// Basic functions
    //    // "abs" => AbsFunctionInfo,

    //    // Fallback
    //    _ => null
    //};
    public abstract FunctionInformation? GetFunctionInformation(string functionName, bool caseSensitive);


}
