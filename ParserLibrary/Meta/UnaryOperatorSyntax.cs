using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Meta;

public sealed class UnaryOperatorSyntax
{
    public int? Scenario { get; init; }

    // Allowed operand types for this unary form (e.g., int, double, bool)
    public required HashSet<Type> OperandTypes { get; init; }

    public required Type OutputType { get; init; }

    public string? Example { get; init; }
    public string? Description { get; init; }

    // args: [operand]; context: optional runtime context
    public Func<object?[], object?, object?>? Calc { get; init; }

    // Per-syntax validation hook (runs after type matching)
    public Func<object?[], ValidationResult>? AdditionalValidation { get; init; }

    public bool IsMatch(Type operand, bool allowParentTypes)
        => OperandTypes.Any(t => TypeHelpers.TypeMatches(operand, t, allowParentTypes));
}

public sealed class UnaryOperatorSyntaxMatch
{
    public required UnaryOperatorSyntax MatchedSyntax { get; init; }
    public required Type OperandType { get; init; }
}