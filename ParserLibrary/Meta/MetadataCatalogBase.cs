using System.Linq.Expressions;
using System.Reflection;

namespace ParserLibrary.Meta;

// Generic catalog base reusable for Function/Unary/Binary catalogs.
// Provides reflection-based enumeration and name/description search.
public abstract class MetadataCatalogBase<TInfo> where TInfo : OperatorInformation
{
    protected List<TInfo>? _allItemsCache;

    public abstract bool RefreshEachTime { get; }

    protected virtual List<TInfo> GetAllCore()
    {
        return GetAllCoreInternal();
    }

    //protected virtual async Task<List<TInfo>> GetAllCoreAsync()
    //{
    //    return await Task.FromResult(GetAllCoreInternal());
    //}

    protected private List<TInfo> GetAllCoreInternal()
    {
        if (_allItemsCache is not null && !RefreshEachTime)
            return _allItemsCache;

        var props = GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(TInfo));

        _allItemsCache = [.. props
            .Select(p => (TInfo)p.GetValue(this)!)
            .OrderBy(GetName, StringComparer.OrdinalIgnoreCase)];

        return _allItemsCache;
    }




    protected List<TInfo> SearchByNameOrDescription(string searchTerm, StringComparison comparisonType)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetAllCore();

        searchTerm = searchTerm.Trim();

        _allItemsCache ??= GetAllCore();

        var all = _allItemsCache;
        var results = all
            .Where(it =>
                GetName(it).Contains(searchTerm, comparisonType) ||
                (GetDescription(it)?.Contains(searchTerm, comparisonType) ?? false))
            .ToList();

        return results;
    }

    // --------- Cached accessors for Name/Description on TInfo ----------
    private static readonly Func<TInfo, string> GetName = BuildStringGetter("Name") ?? (_ => string.Empty);
    private static readonly Func<TInfo, string?> GetDescription = BuildNullableStringGetter("Description");

    private static Func<TInfo, string>? BuildStringGetter(string propName)
    {
        var prop = typeof(TInfo).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || prop.PropertyType != typeof(string)) return null;

        var param = Expression.Parameter(typeof(TInfo), "x");
        var body = Expression.Property(param, prop);
        return Expression.Lambda<Func<TInfo, string>>(body, param).Compile();
    }

    private static Func<TInfo, string?> BuildNullableStringGetter(string propName)
    {
        var prop = typeof(TInfo).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null || (prop.PropertyType != typeof(string) && prop.PropertyType != typeof(string)))
            return _ => null;

        var param = Expression.Parameter(typeof(TInfo), "x");
        var body = Expression.Property(param, prop);
        return Expression.Lambda<Func<TInfo, string?>>(Expression.Convert(body, typeof(string)), param).Compile();
    }
}