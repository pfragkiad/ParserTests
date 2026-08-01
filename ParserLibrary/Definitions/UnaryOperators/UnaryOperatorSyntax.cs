using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.UnaryOperators;

public sealed class UnaryOperatorSyntax
{
    public int? Scenario { get; init; }

    // Allowed operand types for this unary form (e.g., int, double, bool)
    public required HashSet<Type> OperandTypes { get; init; }

    public required Type OutputType { get; init; }

    public HashSet<Type>? PossibleOutputTypes { get; init; }

    // Single example and description
    public string? Description { get; init; }

    // Multiple examples for this syntax
    public string[]? Examples { get; init; }

    // args: [operand]; context: optional runtime context
    public Func<object?, ParserContext?, object?>? Calc { get; init; }

    // args: [operand], context, cancellation token, returns result
    public Func<object?, ParserContext?, CancellationToken, Task<object?>>? CalcAsync { get; init; }

    [JsonIgnore]
    public Func<object?, ParserContext?, Type?>? ResolveOutputTypeFromValues { get; init; }

    [JsonIgnore]
    public Func<object?, ParserContext?, CancellationToken, Task<Type?>>? ResolveOutputTypeFromValuesAsync { get; init; }

    // Per-syntax validation hook (runs after type matching)
    public Func<object?, ParserContext?, ValidationResult>? AdditionalValidation { get; init; }

    // Per-syntax async validation hook (runs after type matching)
    public Func<object?, ParserContext?, CancellationToken, Task<ValidationResult>>? AdditionalValidationAsync { get; init; }

    public Type ResolveOutputTypeOrDefault(object? operand, ParserContext? context)
    {
        return ResolveOutputTypeFromValues?.Invoke(operand, context) ?? OutputType;
    }

    public async Task<Type> ResolveOutputTypeOrDefaultAsync(object? operand, ParserContext? context, CancellationToken ct)
    {
        if (ResolveOutputTypeFromValuesAsync is not null)
            return await ResolveOutputTypeFromValuesAsync(operand, context, ct) ?? OutputType;

        return ResolveOutputTypeOrDefault(operand, context);
    }

    public IEnumerable<Type> GetAllOutputTypes()
    {
        yield return OutputType;

        if (PossibleOutputTypes is null)
            yield break;

        foreach (var type in PossibleOutputTypes)
        {
            if (type != OutputType)
                yield return type;
        }
    }

    public bool HasValueDependentOutputType =>
        ResolveOutputTypeFromValues is not null ||
        ResolveOutputTypeFromValuesAsync is not null ||
        PossibleOutputTypes is { Count: > 0 };

    public bool HasMultipleOutputTypes => PossibleOutputTypes is { Count: > 0 };

    public bool IsMatch(Type operand, bool allowParentTypes)
        => TypeHelpers.MatchesAnyExpectedWithNullAwareness(operand, OperandTypes, allowParentTypes);
}
