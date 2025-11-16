using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Meta;

public sealed class BinaryOperatorSyntax
{
    public int? Scenario { get; init; }

    // Allowed operand sets (per side)
    public required HashSet<Type> LeftTypes { get; init; }
    public required HashSet<Type> RightTypes { get; init; }

    public required Type OutputType { get; init; }

    // Single example and description
    public string? Description { get; init; }

    // Multiple examples for this syntax
    public string[]? Examples { get; init; }

    // args: left, right; context: optional runtime context
    public Func<object?, object?, object?, object?>? Calc { get; init; }

    // Per-syntax validation hook (runs after type matching)
    public Func<object?, object?, ValidationResult>? AdditionalValidation { get; init; }

    public bool IsMatch(Type left, Type right, bool allowParentTypes)
    {
        bool leftOk = LeftTypes.Any(t => TypeHelpers.TypeMatches(left, t, allowParentTypes));
        if (!leftOk) return false;

        bool rightOk = RightTypes.Any(t => TypeHelpers.TypeMatches(right, t, allowParentTypes));
        return rightOk;
    }
}

public sealed class BinaryOperatorSyntaxMatch
{
    public required BinaryOperatorSyntax MatchedSyntax { get; init; }
    public required Type LeftType { get; init; }
    public required Type RightType { get; init; }
}