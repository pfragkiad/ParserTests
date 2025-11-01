using CustomResultError;
using FluentValidation.Results;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ParserLibrary.Meta;

public class FunctionInformation : OperatorInformation
{

    public bool IsCustomFunction { get; init; } = false;

    public byte? MinArgumentsCount { get; init; }
    public byte? MaxArgumentsCount { get; init; }
    public byte? FixedArgumentsCount { get; init; }


    // Types: ignored in JSON to avoid reflection graphs and schema collisions
    [JsonIgnore] public IReadOnlyList<HashSet<Type>>? AllowedTypesPerPosition { get; init; }
    [JsonIgnore] public HashSet<Type>? AllowedTypesForAll { get; init; }
    [JsonIgnore] public HashSet<Type>? AllowedTypesForLast { get; init; }

    // String values (0-based keys internally)
    [JsonIgnore] public Dictionary<int, HashSet<string>>? AllowedStringValuesPerPosition { get; init; }
    public HashSet<string>? AllowedStringValuesForAll { get; init; }
    public HashSet<string>? AllowedStringValuesForLast { get; init; }

    [JsonIgnore] public Dictionary<int, HashSet<string>>? AllowedStringFormatsPerPosition { get; init; }
    public HashSet<string>? AllowedStringFormatsForAll { get; init; }
    public HashSet<string>? AllowedStringFormatsForLast { get; init; }



    [JsonIgnore] public List<FunctionSyntax>? Syntaxes { get; init; }



    public Result<FunctionSyntax, ValidationResult> GetValidSyntax(object?[] args)
    {
        // Require syntaxes to be present
        if (Syntaxes is null || Syntaxes.Count == 0)
            return Helpers.GetFailureResult("function", $"Function '{Name}' has no declared syntaxes.", null);

        // No nulls in arguments
        if (args.Any(a => a is null))
            return Helpers.GetFailureResult("arguments", $"{Name} does not accept null arguments.", null);

        // Resolve argument types (support passing Type directly)
        var resolved = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            resolved[i] = args[i] is Type t ? t : args[i]!.GetType();

        // Try to match any syntax
        foreach (var syn in Syntaxes)
        {
            // 1) Try fixed signature
            if (IsFixedMatch(syn.InputsFixed, resolved))
            {
                var strCheck = ValidateStringConstraints(this, args);
                if (!strCheck.IsValid) return strCheck;
                return syn;
            }

            // 2) Try dynamic signature
            if (IsDynamicMatch(syn.InputsDynamic, resolved))
            {
                var strCheckDyn = ValidateStringConstraints(this, args);
                if (!strCheckDyn.IsValid) return strCheckDyn;
                return syn;
            }
        }

        // Nothing matched
        return Helpers.GetFailureResult("arguments", $"{Name} arguments do not match any declared syntax.", null);
    }

    private static bool IsFixedMatch(List<Type>? inputsFixed, Type[] resolved)
    {
        if (inputsFixed is null)
            return false;

        // Allow zero-args fixed signature when the list is empty
        if (resolved.Length != inputsFixed.Count)
            return false;

        for (int i = 0; i < inputsFixed.Count; i++)
        {
            var expected = inputsFixed[i];
            var actual = resolved[i];
            if (!ReferenceEquals(expected, actual))
                return false;
        }

        return true;
    }

    private static bool IsDynamicMatch(InputsDynamic? inputsDynamic, Type[] resolved)
    {
        if (!inputsDynamic.HasValue)
            return false;

        var dyn = inputsDynamic.Value;
        var hasFirst = dyn.FirstInputType is not null;
        var hasLast = dyn.LastInputType is not null;
        var middleSet = dyn.MiddleInputTypes; // can be null/empty
        var minVar = dyn.MinVariableArgumentsCount;

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
            if (!ReferenceEquals(resolved[0], dyn.FirstInputType))
                return false;
            start = 1;
        }

        // Check last
        if (hasLast)
        {
            if (!ReferenceEquals(resolved[^1], dyn.LastInputType))
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
                if (!middleSet.Contains(resolved[i]))
                    return false;
            }
        }

        return true;
    }

    public static ValidationResult ValidateStringConstraints(FunctionInformation info, object?[] callArgs)
    {
        for (int i = 0; i < callArgs.Length; i++)
        {
            if (callArgs[i] is not string strArg) continue;

            // Values
            HashSet<string>? allowedValues = null;
            if (info.AllowedStringValuesForLast is { Count: > 0 } && i == callArgs.Length - 1)
                allowedValues = info.AllowedStringValuesForLast;
            else if (info.AllowedStringValuesPerPosition is not null && info.AllowedStringValuesPerPosition.TryGetValue(i, out var set) && set.Count > 0)
                allowedValues = set;
            else if (info.AllowedStringValuesForAll is { Count: > 0 })
                allowedValues = info.AllowedStringValuesForAll;

            if (allowedValues is not null && allowedValues.Count > 0)
            {
                if (!allowedValues.Contains(strArg, StringComparer.OrdinalIgnoreCase))
                {
                    string posText = Helpers.ToOrdinal(i + 1);
                    return Helpers.GetFailureResult(
                        "arguments",
                        $"{info.Name} function allowed string values for the {posText} argument are [{string.Join(", ", allowedValues)}], got '{strArg}'.",
                        strArg);
                }
            }

            // Formats (regex)
            HashSet<string>? allowedFormats = null;
            if (info.AllowedStringFormatsForLast is { Count: > 0 } && i == callArgs.Length - 1)
                allowedFormats = info.AllowedStringFormatsForLast;
            else if (info.AllowedStringFormatsPerPosition is not null && info.AllowedStringFormatsPerPosition.TryGetValue(i, out var fmtSet) && fmtSet.Count > 0)
                allowedFormats = fmtSet;
            else if (info.AllowedStringFormatsForAll is { Count: > 0 })
                allowedFormats = info.AllowedStringFormatsForAll;

            if (allowedFormats is not null && allowedFormats.Count > 0)
            {
                bool matches = allowedFormats.Any(fmt =>
                    !string.IsNullOrEmpty(fmt) &&
                    Regex.IsMatch(strArg, fmt, RegexOptions.IgnoreCase));
                if (!matches)
                {
                    string posText = Helpers.ToOrdinal(i + 1);
                    return Helpers.GetFailureResult(
                        "arguments",
                        $"{info.Name} function allowed string formats for the {posText} argument are [{string.Join(", ", allowedFormats)}], got '{strArg}'.",
                        strArg);
                }
            }
        }
        return new ValidationResult();
    }

    public FunctionDefinitionDto ToDefinitionDto() =>
        FunctionDefinitionDto.From(this);
}


