using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Channels;
using ParserLibrary.Parsers;

namespace ParserLibrary.Meta;

public class OperatorDefinition
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

    // ----------------- Shared helpers for building syntax descriptions -----------------
    // Used by FunctionInformation, BinaryOperatorInformation and UnaryOperatorInformation
    protected string BuildSyntaxesDescription<TSyn>(IEnumerable<TSyn>? syntaxes, Func<TSyn, string> describe)
    {
        if (syntaxes is null) return "  (none)";
        var lines = new List<string>();
        foreach (var s in syntaxes)
            lines.Add(describe(s));
        return string.Join(Environment.NewLine, lines);
    }

    protected static string FormatTypeName(Type t) => TypeNameDisplay.GetDisplayTypeName(t);

    protected static string FormatTypeSet(IEnumerable<Type>? types)
    {
        if (types is null) return "-";
        var arr = types.ToArray();
        if (arr.Length == 0) return "-";
        if (arr.Length == 1) return FormatTypeName(arr[0]);
        return "[" + string.Join("|", arr.Select(FormatTypeName)) + "]";
    }
}
