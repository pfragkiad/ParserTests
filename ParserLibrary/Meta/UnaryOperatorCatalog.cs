using System.Reflection;

namespace ParserLibrary.Meta;

public abstract class UnaryOperatorCatalog : MetadataCatalogBase<UnaryOperatorInformation>
{
    // Example property pattern (one operator form per instance):
    /*
    private UnaryOperatorInformation? _plusUnaryPrefix;
    public UnaryOperatorInformation PlusUnaryPrefix => (_plusUnaryPrefix ??= new()
    {
        Name = "+",
        Kind = UnaryOperatorKind.Prefix,
        Description = "Unary plus (prefix).",
        Examples = [ new() { Syntax = "+a", Description = "Unary plus" } ],
        AllowedOperandTypes = [ typeof(int), typeof(double) ]
    }).Value;
    */

    public List<UnaryOperatorInformation> GetAllOperators() => GetAllCore();

    public List<UnaryOperatorInformation> SearchOperators(string searchTerm, StringComparison comparisonType)
        => SearchByNameOrDescription(searchTerm, comparisonType);

    // Convenience helper for implementers that prefer not to hardcode switches:
    protected UnaryOperatorInformation? FindOperatorInformation(string operatorNameOrSymbol, bool caseSensitive, UnaryOperatorKind kind)
    {
        if (string.IsNullOrWhiteSpace(operatorNameOrSymbol))
            return null;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var oi in GetAllCore())
        {
            if (oi.Name.Equals(operatorNameOrSymbol, comparison) && oi.Kind == kind)
                return oi;
        }
        return null;
    }

    // Example dispatch:
    // string op = caseSensitive ? symbol : symbol.ToLowerInvariant();
    // return (op, kind) switch
    // {
    //     ("+", UnaryOperatorKind.Prefix) => PlusUnaryPrefix,
    //     ("-", UnaryOperatorKind.Prefix) => MinusUnaryPrefix,
    //     _ => null
    // };
    public abstract UnaryOperatorInformation? GetOperatorInformation(string operatorNameOrSymbol, bool caseSensitive, UnaryOperatorKind kind);
}