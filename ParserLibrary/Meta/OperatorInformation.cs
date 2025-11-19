using System.Threading.Channels;

namespace ParserLibrary.Meta;

public class OperatorInformation
{
    public int? Id { get; init; }

    public required string Name { get; init; }

    public string[]? Aliases { get; init; }

    public override string ToString() => Id.HasValue ?
        $"{Name} (ID: {Id})" : Name;

    public string? Description { get; init; }
    public IList<SyntaxExample>? Examples { get; init; }


    public static Type GetArgumentType(object? o)
    {
        if (o is null) return typeof(object);
        if (o is Type t) return t;
        return o.GetType();
    }

}
