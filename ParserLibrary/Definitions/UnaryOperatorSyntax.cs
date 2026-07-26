using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Definitions;

public sealed class UnaryOperatorSyntax
{
    public int? Scenario { get; init; }

    // Allowed operand types for this unary form (e.g., int, double, bool)
    public required HashSet<Type> OperandTypes { get; init; }

    public required Type OutputType { get; init; }

    // Single example and description
    public string? Description { get; init; }

    // Multiple examples for this syntax
    public string[]? Examples { get; init; }

    // args: [operand]; context: optional runtime context
    public Func<object?, ParserContext?, object?>? Calc { get; init; }

    // args: [operand], context, cancellation token, returns result
    public Func<object?, ParserContext?, CancellationToken, Task<object?>>? CalcAsync { get; init; }

    // Per-syntax validation hook (runs after type matching)
    public Func<object?, ParserContext?, ValidationResult>? AdditionalValidation { get; init; }

    // Per-syntax async validation hook (runs after type matching)
    public Func<object?, ParserContext?, CancellationToken, Task<ValidationResult>>? AdditionalValidationAsync { get; init; }

    public bool IsMatch(Type operand, bool allowParentTypes)
        => TypeHelpers.MatchesAnyExpectedWithNullAwareness(operand, OperandTypes, allowParentTypes);
}

public sealed class UnaryOperatorSyntaxMatch
{
    public required UnaryOperatorSyntax MatchedSyntax { get; init; }
    public required Type OperandType { get; init; }
}