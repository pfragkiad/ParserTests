using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Definitions;

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
    public Func<object?, object?, ParserContext?, object?>? Calc { get; init; }

    // args: left, right, context, cancellation token, returns result
    public Func<object?, object?, ParserContext?, CancellationToken, Task<object?>>? CalcAsync { get; init; }

    // Per-syntax validation hook (runs after type matching)
    public Func<object?, object?, ValidationResult>? AdditionalValidation { get; init; }

    // Per-syntax async validation hook (runs after type matching)
    public Func<object?, object?, ParserContext?, CancellationToken, Task<ValidationResult>>? AdditionalValidationAsync { get; init; }

    public bool IsMatch(Type left, Type right, bool allowParentTypes)
    {
        // Use null-aware type matching to ensure that null (object) only matches
        // syntaxes that explicitly declare object in their allowed types
        bool leftOk = TypeHelpers.MatchesAnyExpectedWithNullAwareness(left, LeftTypes, allowParentTypes);
        if (!leftOk) return false;

        bool rightOk = TypeHelpers.MatchesAnyExpectedWithNullAwareness(right, RightTypes, allowParentTypes);
        return rightOk;
    }
}

public sealed class BinaryOperatorSyntaxMatch
{
    public required BinaryOperatorSyntax MatchedSyntax { get; init; }
    public required Type LeftType { get; init; }
    public required Type RightType { get; init; }
}