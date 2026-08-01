using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.BinaryOperators;

public sealed class BinaryOperatorSyntax
{
    public int? Scenario { get; init; }

    // Allowed operand sets (per side)
    public required HashSet<Type> LeftTypes { get; init; }
    public required HashSet<Type> RightTypes { get; init; }

    public required Type OutputType { get; init; }

    public HashSet<Type>? PossibleOutputTypes { get; init; }

    // Single example and description
    public string? Description { get; init; }

    // Multiple examples for this syntax
    public string[]? Examples { get; init; }

    // args: left, right; context: optional runtime context
    public Func<object?, object?, ParserContext?, object?>? Calc { get; init; }

    // args: left, right, context, cancellation token, returns result
    public Func<object?, object?, ParserContext?, CancellationToken, Task<object?>>? CalcAsync { get; init; }

    [JsonIgnore]
    public Func<object?, object?, ParserContext?, Type?>? ResolveOutputTypeFromValues { get; init; }

    [JsonIgnore]
    public Func<object?, object?, ParserContext?, CancellationToken, Task<Type?>>? ResolveOutputTypeFromValuesAsync { get; init; }

    // Per-syntax validation hook (runs after type matching)
    public Func<object?, object?, ValidationResult>? AdditionalValidation { get; init; }

    // Per-syntax async validation hook (runs after type matching)
    public Func<object?, object?, ParserContext?, CancellationToken, Task<ValidationResult>>? AdditionalValidationAsync { get; init; }

    public Type ResolveOutputTypeOrDefault(object? left, object? right, ParserContext? context)
    {
        return ResolveOutputTypeFromValues?.Invoke(left, right, context) ?? OutputType;
    }

    public async Task<Type> ResolveOutputTypeOrDefaultAsync(object? left, object? right, ParserContext? context, CancellationToken ct)
    {
        if (ResolveOutputTypeFromValuesAsync is not null)
            return await ResolveOutputTypeFromValuesAsync(left, right, context, ct) ?? OutputType;

        return ResolveOutputTypeOrDefault(left, right, context);
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
