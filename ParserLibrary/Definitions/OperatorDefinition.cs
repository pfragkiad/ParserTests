using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Definitions;

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
        if (o is Type t)
            return TypeHelpers.NormalizeNullMarkerType(t);

        return TypeHelpers.ResolveRuntimeArgumentType(o);
    }

    protected static Type[] ResolveArgumentTypes(object?[] args)
    {
        var resolved = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            resolved[i] = GetArgumentType(args[i]);
        return resolved;
    }

    protected static Result<TSyntax, ValidationResult> FindMatchingSyntax<TSyntax>(
        IList<TSyntax>? syntaxes,
        Func<TSyntax, bool> isMatch,
        Func<TSyntax, ValidationResult>? validateMatchedSyntax,
        string noSyntaxCategory,
        string noSyntaxMessage,
        Func<ValidationResult> noMatchValidationFactory)
    {
        if (syntaxes is null || syntaxes.Count == 0)
            return ValidationHelpers.FailureResult(noSyntaxCategory, noSyntaxMessage, null);

        foreach (var syntax in syntaxes)
        {
            if (!isMatch(syntax)) continue;

            if (validateMatchedSyntax is not null)
            {
                var validation = validateMatchedSyntax(syntax);
                if (!validation.IsValid) return validation;
            }

            return syntax;
        }

        return noMatchValidationFactory();
    }

    // ----------------- Shared helpers for building syntax descriptions -----------------
    // Used by FunctionInformation, BinaryOperatorInformation and UnaryOperatorInformation
    protected static string BuildSyntaxesDescription<TSyn>(IEnumerable<TSyn>? syntaxes, Func<TSyn, string> describe)
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
