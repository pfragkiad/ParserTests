using CustomResultError;
using FluentValidation.Results;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ParserLibrary.Meta;

public readonly struct FunctionSyntax
{
    public int? Scenario { get; init; }

    public List<Type>? InputsFixed { get; init; }


    public Type? FirstInputType { get; init; }
    public HashSet<Type>? InputsDynamic { get; init; }
    public Type? LastInputType { get; init; }

    public Type OutputType { get; init; }

    public string? Example { get; init; }
    public string? Description { get; init; }
}

public readonly struct FunctionInformation
{
    public int? Id { get; init; }

    public string Name { get; init; }

    public override string ToString() => Id.HasValue ?
        $"{Name} (ID: {Id})" : Name;

    public string? Description { get; init; }
    public byte? MinArgumentsCount { get; init; }
    public byte? MaxArgumentsCount { get; init; }
    public byte? FixedArgumentsCount { get; init; }

    public IList<SyntaxExample>? Examples { get; init; }

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
            // Fixed signature: exact arity and type match per position
            if (syn.InputsFixed is { Count: > 0 })
            {
                if (resolved.Length != syn.InputsFixed.Count)
                    continue;

                bool allMatch = true;
                for (int i = 0; i < syn.InputsFixed.Count; i++)
                {
                    var expected = syn.InputsFixed[i];
                    var actual = resolved[i];
                    if (!ReferenceEquals(expected, actual))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (!allMatch) continue;

                // Validate string values/formats
                var strCheck = ValidateStringConstraints(this, args);
                if (!strCheck.IsValid) return strCheck;
                return syn;
            }

            // Dynamic: [FirstInputType] (zero-or-more middles from InputsDynamic) [LastInputType]
            var hasFirst = syn.FirstInputType is not null;
            var hasLast = syn.LastInputType is not null;
            var dynamicSet = syn.InputsDynamic; // can be null/empty

            // Boundary feasibility
            if (hasFirst && resolved.Length < 1)
                continue;
            if (hasLast && resolved.Length < (hasFirst ? 2 : 1))
                continue;

            int start = 0, end = resolved.Length - 1;

            if (hasFirst)
            {
                if (!ReferenceEquals(resolved[0], syn.FirstInputType))
                    continue;
                start = 1;
            }

            if (hasLast)
            {
                if (!ReferenceEquals(resolved[^1], syn.LastInputType))
                    continue;
                end = resolved.Length - 2;
            }

            // Validate middles: must all belong to InputsDynamic (if there are middles)
            if (start <= end)
            {
                if (dynamicSet is null || dynamicSet.Count == 0)
                    continue;

                bool middlesOk = true;
                for (int i = start; i <= end; i++)
                {
                    if (!dynamicSet.Contains(resolved[i]))
                    {
                        middlesOk = false;
                        break;
                    }
                }
                if (!middlesOk) continue;
            }

            // String constraints (values + formats)
            var strCheckDyn = ValidateStringConstraints(this, args);
            if (!strCheckDyn.IsValid) return strCheckDyn;

            return syn;
        }

        // Nothing matched
        return Helpers.GetFailureResult("arguments", $"{Name} arguments do not match any declared syntax.", null);

        // Applies string value and format constraints:
        // - Per-position dictionaries (0-based)
        // - For-all and For-last sets
        static ValidationResult ValidateStringConstraints(FunctionInformation info, object?[] callArgs)
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
    }

}
