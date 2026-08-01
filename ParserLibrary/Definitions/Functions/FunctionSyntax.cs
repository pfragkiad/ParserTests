using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions.Functions;

public class FunctionSyntax
{
    /// <summary>
    /// Scenario ID is optional but can assist in direct syntax identification if combined with an enum code value.
    /// </summary>
    public int? Scenario { get; init; }

    public string? Expression { get; init; } //useful for custom functions only

    public string? ExpressionClean { get; init; }


    //should be initialized to EMPTY array if no inputs at all
    public List<HashSet<Type>>? InputsFixed { get; init; }

    public bool IsEmpty => (InputsFixed is null || InputsFixed!.Count == 0) && (InputsDynamic is null || InputsDynamic!.Value.MiddleInputTypes.Count==0);

    public InputsDynamic? InputsDynamic { get; init; }

    public required Type OutputType { get; init; }

    public HashSet<Type>? PossibleOutputTypes { get; init; }

    public string[]? Examples { get; init; }

    public string? Description { get; init; }

    public Func<object?[],ParserContext?, object?>? Calc { get; init; } //args, context, returns result (IF NULL THEN USE CALCASYNC?)

    //args, context, cancellation token, returns result
    public Func<object?[], ParserContext?, CancellationToken, Task<object?>>? CalcAsync { get; init; }

    [JsonIgnore]
    public Func<object?[], ParserContext?, Type?>? ResolveOutputTypeFromValues { get; init; }

    [JsonIgnore]
    public Func<object?[], ParserContext?, CancellationToken, Task<Type?>>? ResolveOutputTypeFromValuesAsync { get; init; }


    //args, context, returns result
    [JsonIgnore]
    public Func<object?[], ParserContext?, ValidationResult>? AdditionalValidation { get; init; }


    //args, context, cancellation token, returns ValidationResult
    [JsonIgnore]
    public Func<object?[], ParserContext?, CancellationToken, Task<ValidationResult>>? AdditionalValidationAsync { get; init; }

    public Type ResolveOutputTypeOrDefault(object?[] args, ParserContext? context)
    {
        return ResolveOutputTypeFromValues?.Invoke(args, context) ?? OutputType;
    }

    public async Task<Type> ResolveOutputTypeOrDefaultAsync(object?[] args, ParserContext? context, CancellationToken ct)
    {
        if (ResolveOutputTypeFromValuesAsync is not null)
            return await ResolveOutputTypeFromValuesAsync(args, context, ct) ?? OutputType;

        return ResolveOutputTypeOrDefault(args, context);
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

    public static FunctionSyntax CreateEmpty(Type outputType, int? scenarioId, string? description = null, params string[] examples)
    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsFixed = [],
            OutputType = outputType,
            Examples = examples,
            Description = description
        };
    }

    // New: fixed with multi-type positions
    public static FunctionSyntax CreateFixed(List<HashSet<Type>> inputTypeSets, Type outputType, int? scenarioId, string? description = null, params string[] examples)
    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsFixed = inputTypeSets,
            OutputType = outputType,
            Examples = examples,
            Description = description
        };
    }

    // Back-compat convenience: single-type positions
    public static FunctionSyntax CreateFixed(List<Type> inputTypes, Type outputType, int? scenarioId, string? description = null, params string[] examples)    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsFixed = inputTypes.Select(t => new HashSet<Type> { t }).ToList(),
            OutputType = outputType,
            Examples = examples,
            Description = description
        };
    }

    // New: dynamic with multi-type first/last
    public static FunctionSyntax CreateVariable(
        byte minVariableArgsCount,
        HashSet<Type>? firstInputTypes,
        HashSet<Type> middleInputTypes, //at least one middle input type or else it is not variable
        HashSet<Type>? lastInputTypes,
        Type outputType,
        string? description = null,
        int? scenarioId = null,
        params string[] examples)
    {
        return new FunctionSyntax
        {
            Scenario = scenarioId,
            InputsDynamic = new InputsDynamic
            {
                FirstInputType = firstInputTypes,
                MiddleInputTypes = middleInputTypes,
                LastInputType = lastInputTypes,
                MinMiddleArgumentsCount = minVariableArgsCount
            },
            OutputType = outputType,
            Examples = examples,
            Description = description
        };
    }

    public bool IsFixedMatch(Type[] resolved, bool allowParentTypes)
    {
        if (InputsFixed is null)
            return false;

        // Must match arity exactly (zero-args supported with empty list)
        if (resolved.Length != InputsFixed.Count)
            return false;

        for (int i = 0; i < InputsFixed.Count; i++)
        {
            var expectedSet = InputsFixed[i];
            var actual = resolved[i];

            // Use null-aware type matching to ensure that null (object) only matches
            // syntaxes that explicitly declare object in their allowed types
            if (!TypeHelpers.MatchesAnyExpectedWithNullAwareness(actual, expectedSet, allowParentTypes))
                return false;
        }

        return true;
    }

    public bool IsDynamicMatch(Type[] resolved, bool allowParentTypes)
    {
        if (!InputsDynamic.HasValue)
            return false;

        var dyn = InputsDynamic.Value;
        var hasFirst = dyn.FirstInputType is not null;
        var hasLast = dyn.LastInputType is not null;
        var middleSet = dyn.MiddleInputTypes; // can be empty
        var minVar = dyn.MinMiddleArgumentsCount;

        // Boundary feasibility
        if (hasFirst && resolved.Length < 1)
            return false;
        if (hasLast && resolved.Length < (hasFirst ? 2 : 1))
            return false;

        int start = 0;
        int endExclusive = resolved.Length;

        // Check first (use null-aware matching)
        if (hasFirst)
        {
            if (!TypeHelpers.MatchesAnyExpectedWithNullAwareness(resolved[0], dyn.FirstInputType!, allowParentTypes))
                return false;
            start = 1;
        }

        // Check last (use null-aware matching)
        if (hasLast)
        {
            if (!TypeHelpers.MatchesAnyExpectedWithNullAwareness(resolved[^1], dyn.LastInputType!, allowParentTypes))
                return false;
            endExclusive = resolved.Length - 1;
        }

        // Middle segment
        int middleCount = endExclusive - start;

        // Enforce minimum number of middle arguments (if specified)
        if (minVar > 0 && middleCount < minVar)
            return false;

        // Validate middles: must all belong to MiddleInputTypes when there are middles (use null-aware matching)
        if (middleCount > 0)
        {
            if (middleSet is null || middleSet.Count == 0)
                return false;

            for (int i = start; i < endExclusive; i++)
            {
                var actualMid = resolved[i];
                // Use null-aware matching: any expected type that matches (supports inheritance if allowed)
                if (!TypeHelpers.MatchesAnyExpectedWithNullAwareness(actualMid, middleSet, allowParentTypes))
                    return false;
            }
        }

        return true;
    }

    public HashSet<string>? Tags { get; init; }

}
