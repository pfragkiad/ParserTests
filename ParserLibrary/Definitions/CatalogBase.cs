using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace ParserLibrary.Definitions;

// Generic catalog base reusable for Function/Unary/Binary catalogs.
// Provides reflection-based enumeration and name/description search.
public abstract class CatalogBase<TInfo> where TInfo : OperatorDefinition
{
    protected List<TInfo>? _allItemsCache;

    // Now maps a name/alias to a single TInfo (preferred entry)
    protected Dictionary<string, TInfo>? _itemsDictionaryCache;

    public abstract bool RefreshEachTime { get; }

    // Catalog-level policy: whether lookups should ignore case by default
    public abstract bool IgnoreCase { get; }

    public virtual List<TInfo> GetAll()
    {
        return GetAllInternal();
    }

    //protected virtual async Task<List<TInfo>> GetAllCoreAsync()
    //{
    //    return await Task.FromResult(GetAllCoreInternal());
    //}

    protected private List<TInfo> GetAllInternal()
    {
        if (_allItemsCache is not null && !RefreshEachTime)
            return _allItemsCache;

        var props = GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(TInfo));

        // order using catalog's IgnoreCase policy
        var nameComparer = IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _allItemsCache = [.. props
            .Select(p => (TInfo)p.GetValue(this)!)
            .OrderBy(it => it.Name, nameComparer)
        ];

        // Build dictionary keyed by Name and Aliases using the catalog-level comparer
        var dict = new Dictionary<string, TInfo>(IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        // For each key we keep the first encountered TInfo (ordered above). This avoids lists and
        // gives a single deterministic entry per key (name or alias).
        foreach (var it in _allItemsCache)
        {
            if (it is null) continue;

            var name = it.Name;
            if (!string.IsNullOrWhiteSpace(name) && !dict.ContainsKey(name))
            {
                dict[name] = it;
            }

            if (it.Aliases is not null)
            {
                foreach (var a in it.Aliases)
                {
                    if (string.IsNullOrWhiteSpace(a)) continue;
                    if (!dict.ContainsKey(a))
                    {
                        dict[a] = it;
                    }
                }
            }
        }

        _itemsDictionaryCache = dict;

        return _allItemsCache;
    }


    public List<TInfo> SearchByNameOrDescription(string searchTerm, StringComparison comparisonType)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetAll();

        searchTerm = searchTerm.Trim();

        _allItemsCache ??= GetAll();

        var all = _allItemsCache;
        var results = all
            .Where(it =>
                it.Name.Contains(searchTerm, comparisonType) ||
                (it.Description?.Contains(searchTerm, comparisonType) ?? false))
            .ToList();

        return results;
    }

    // Simple helper: return single match for name (uses the internal dictionary's comparer)
    public virtual TInfo? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Ensure caches/dictionary are built
        _allItemsCache ??= GetAll();

        if (_itemsDictionaryCache is not null)
        {
            if (_itemsDictionaryCache.TryGetValue(name, out var match))
                return match;
            return null;
        }

        // Defensive fallback if dictionary isn't available: linear scan using catalog-level IgnoreCase
        var comparison = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return _allItemsCache.FirstOrDefault(it =>
            it.Name.Equals(name, comparison) ||
            (it.Aliases?.Any(a => a.Equals(name, comparison)) ?? false));
    }


}