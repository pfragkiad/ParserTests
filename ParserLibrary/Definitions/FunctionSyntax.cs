using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions;


public readonly struct InputsDynamic
{
    //should be used only if different from InputsDynamic
    public HashSet<Type>? FirstInputType { get; init; }
    //if first inputtype present, then use for >=1, else >=0
    //if last inputtype present, then use until n-2 else until n-1
    public HashSet<Type> MiddleInputTypes { get; init; }

    //should be used only if different from InputsDynamic
    public HashSet<Type>? LastInputType { get; init; }
    public byte MinMiddleArgumentsCount { get; init; }

}

public class FunctionSyntax
{
    public int? Scenario { get; init; }

    public string? Expression { get; init; } //useful for custom functions only

    public string? ExpressionClean { get; init; }


    //should be initialized to EMPTY array if no inputs at all
    public List<HashSet<Type>>? InputsFixed { get; init; }

    public bool IsEmpty => (InputsFixed is null || InputsFixed!.Count == 0) && (InputsDynamic is null || InputsDynamic!.Value.MiddleInputTypes.Count==0);

    public InputsDynamic? InputsDynamic { get; init; }

    public required Type OutputType { get; init; }

    public string[]? Examples { get; init; }

    public string? Description { get; init; }

    public Func<object?[],object?, object?>? Calc { get; init; } //args, context, returns result

    [JsonIgnore]
    public Func<object?[], ValidationResult>? AdditionalValidation { get; init; }


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

            // any expected type in the set that matches
            if (!expectedSet.Any(exp => TypeHelpers.TypeMatches(actual, exp, allowParentTypes)))
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

        // Check first
        if (hasFirst)
        {
            if (!dyn.FirstInputType!.Any(ft => TypeHelpers.TypeMatches(resolved[0], ft, allowParentTypes)))
                return false;
            start = 1;
        }

        // Check last
        if (hasLast)
        {
            if (!dyn.LastInputType!.Any(lt => TypeHelpers.TypeMatches(resolved[^1], lt, allowParentTypes)))
                return false;
            endExclusive = resolved.Length - 1;
        }

        // Middle segment
        int middleCount = endExclusive - start;

        // Enforce minimum number of middle arguments (if specified)
        if (minVar > 0 && middleCount < minVar)
            return false;

        // Validate middles: must all belong to MiddleInputTypes when there are middles
        if (middleCount > 0)
        {
            if (middleSet is null || middleSet.Count == 0)
                return false;

            for (int i = start; i < endExclusive; i++)
            {
                var actualMid = resolved[i];
                // any expected type that matches (supports inheritance if allowed)
                if (!middleSet.Any(expectedMid => TypeHelpers.TypeMatches(actualMid, expectedMid, allowParentTypes)))
                    return false;
            }
        }

        return true;
    }

}
