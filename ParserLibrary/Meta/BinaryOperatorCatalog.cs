using System.Reflection;

namespace ParserLibrary.Meta;

public abstract class BinaryOperatorCatalog : MetadataCatalogBase<BinaryOperatorInformation>
{
    // Example property pattern (one operator form per instance):
    /*
    private BinaryOperatorInformation? _plusBinary;
    public BinaryOperatorInformation PlusBinary => (_plusBinary ??= new()
    {
        Name = "+",
        Description = "Addition (binary).",
        Examples = [ new() { Syntax = "a + b", Description = "Binary addition" } ],
        AllowedTypePairs =
        [
            (typeof(int), typeof(int)),
            (typeof(double), typeof(double))
        ]
    }).Value;
    */

    public List<BinaryOperatorInformation> GetAllOperators() => GetAllCore();

    public List<BinaryOperatorInformation> SearchOperators(string searchTerm, StringComparison comparisonType)
        => SearchByNameOrDescription(searchTerm, comparisonType);

    // Convenience helper for implementers that prefer not to hardcode switches:
    protected BinaryOperatorInformation? FindOperatorInformation(string operatorNameOrSymbol, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(operatorNameOrSymbol))
            return null;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var oi in GetAllCore())
        {
            if (oi.Name.Equals(operatorNameOrSymbol, comparison))
                return oi;
        }
        return null;
    }

    // Example dispatch:
    // string op = caseSensitive ? symbol : symbol.ToLowerInvariant();
    // return op switch
    // {
    //     "+" => PlusBinary,
    //     "-" => MinusBinary,
    //     _ => null
    // };
    public abstract BinaryOperatorInformation? GetOperatorInformation(string operatorNameOrSymbol, bool caseSensitive);
}