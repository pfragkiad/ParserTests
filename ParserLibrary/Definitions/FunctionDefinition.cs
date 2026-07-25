using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Definitions;

public class SyntaxMatch
{
    public required FunctionSyntax MatchedSyntax { get; init; }

    public Type[] ResolvedTypes { get; init; } = [];
}

public class ExpectedFunctionArgumentsCount
{
    public IList<int>? FixedCounts { get; init; } = [];
    public int? MinCount { get; init; }
}

public class FunctionDefinition : OperatorDefinition
{

    public bool IsCustomFunction { get; init; } = false;

    // String values (0-based keys internally)
    public Dictionary<int, HashSet<string>>? AllowedStringValuesPerPosition { get; init; }
    public HashSet<string>? AllowedStringValuesForAll { get; init; }
    public HashSet<string>? AllowedStringValuesForLast { get; init; }

    public Dictionary<int, HashSet<string>>? AllowedStringFormatsPerPosition { get; init; }
    public HashSet<string>? AllowedStringFormatsForAll { get; init; }
    public HashSet<string>? AllowedStringFormatsForLast { get; init; }



    public List<FunctionSyntax>? Syntaxes { get; init; }

    public ExpectedFunctionArgumentsCount? GetExpectedArgumentsCountFromSyntaxes()
    {
        // Prefer syntax-based discovery when syntaxes exist
        if (Syntaxes is not { Count: > 0 }) return null;


        // Collect distinct fixed arities from fixed signatures (0 included for zero-arg syntaxes)
        var fixedCounts = Syntaxes
            .Where(s => s.InputsFixed is not null)
            .Select(s => s.InputsFixed!.Count)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        int? minDynamicTotal = null;
        foreach (var s in Syntaxes)
        {
            if (!s.InputsDynamic.HasValue) continue;
            var dyn = s.InputsDynamic.Value;

            var hasFirst = dyn.FirstInputType is { Count: > 0 };
            var hasLast = dyn.LastInputType is { Count: > 0 };

            int minTotal = (hasFirst ? 1 : 0) + (hasLast ? 1 : 0) + dyn.MinMiddleArgumentsCount;
            if (minDynamicTotal is null || minTotal < minDynamicTotal)
                minDynamicTotal = minTotal;
        }

        return new ExpectedFunctionArgumentsCount
        {
            FixedCounts = fixedCounts.Count > 0 ? fixedCounts : null,
            MinCount = minDynamicTotal
        };

    }



    public Func<object?[], Result<SyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object?[] args)
    {
        var result = ValidateArgumentTypes(args);
        if (result.IsFailure) return result.Error!;
        var syntaxMatch = result.Value!;
        return syntaxMatch.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(object?[] args, object? context, bool allowParentTypes = true) //only Calc, no async
    {
        var syntaxMatch = ValidateArgumentTypes(args, allowParentTypes);
        if (syntaxMatch.IsFailure) return syntaxMatch.Error!;

        var syntax = syntaxMatch.Value!.MatchedSyntax;
        return syntax.Calc!(args, context);
    }

    //both CalcAsync and Calc support
    public async Task<Result<object?, ValidationResult>> ValidateAndCalcAsync(object?[] args, object? context, CancellationToken ct, bool allowParentTypes = true) 
    {
        var syntaxMatch = ValidateArgumentTypes(args, allowParentTypes);
        if (syntaxMatch.IsFailure) return syntaxMatch.Error!;


        //only Calc can be async for the moment
        var syntax = syntaxMatch.Value!.MatchedSyntax;
        if (syntax.CalcAsync is not null)
        {
            return await syntax.CalcAsync(args, context, ct);
        }
        else if (syntax.Calc is not null)
        {
            return syntax.Calc(args, context);
        }
        else
        {
            return ValidationHelpers.FailureResult("function", $"Function '{Name}' has no calculation method.", null);
        }
    }



    public ValidationResult Validate(object?[] args)
    {
        var result = ValidateArgumentTypes(args);
        return result.Match(_ => ValidationHelpers.Success, err => err);
    }

    // Reuse GetValidSyntax internally; Apply AdditionalValidation and return resolved types + matched syntax
    public Result<SyntaxMatch, ValidationResult> ValidateArgumentTypes(object?[] args, bool allowParentTypes = true)
    {
        // Use the single source of truth for matching and string constraints
        var syntaxResult = GetValidSyntax(args, allowParentTypes);
        if (syntaxResult.IsFailure) return syntaxResult.Error!;

        // Additional business validation after syntax and string checks
        if (AdditionalGlobalValidation is not null)
        {
            var addVal = AdditionalGlobalValidation(args);
            if (addVal.IsFailure) return addVal.Error!;
        }

        // Resolve argument types (support passing Type directly) for the return payload
        var resolved = ResolveArgumentTypes(args);

        return new SyntaxMatch
        {
            MatchedSyntax = syntaxResult.Value!,
            ResolvedTypes = resolved
        };
    }

    // Centralized matcher: validates syntaxes, nulls, type compatibility (with inheritance), and string constraints
    public Result<FunctionSyntax, ValidationResult> GetValidSyntax(object?[] args, bool allowParentTypes = true)
    {
        // Resolve argument types (support passing Type directly)
        var resolved = ResolveArgumentTypes(args);

        return FindMatchingSyntax(
            Syntaxes,
            syn => syn.IsFixedMatch(resolved, allowParentTypes) || syn.IsDynamicMatch(resolved, allowParentTypes),
            syn =>
            {
                var strCheck = ValidateStringConstraints(args);
                if (!strCheck.IsValid) return strCheck;

                if (syn.AdditionalValidation is not null)
                {
                    var addVal = syn.AdditionalValidation(args);
                    if (!addVal.IsValid) return addVal;
                }

                return ValidationHelpers.Success;
            },
            noSyntaxCategory: "function",
            noSyntaxMessage: $"Function '{Name}' has no declared syntaxes.",
            noMatchValidationFactory: () =>
            {
                var resolvedNames = resolved.Length == 0
                    ? "<no arguments>"
                    : string.Join(", ", resolved.Select(TypeNameDisplay.GetDisplayTypeName));

                string syntaxesDescription = BuildSyntaxesDescription(Syntaxes, syn =>
                {
                    string scenarioPart = syn.Scenario.HasValue ? $"(Scenario {syn.Scenario}) " : "";
                    if (syn.InputsFixed is { Count: > 0 })
                    {
                        var fixedParts = syn.InputsFixed!
                            .Select(set => set.Count == 1
                                ? TypeNameDisplay.GetDisplayTypeName(set.First())
                                : "[" + string.Join("|", set.Select(TypeNameDisplay.GetDisplayTypeName)) + "]")
                            .ToArray();
                        return $"  {scenarioPart}Fixed: ({string.Join(", ", fixedParts)}) -> {TypeNameDisplay.GetDisplayTypeName(syn.OutputType)}";
                    }
                    else if (syn.InputsDynamic.HasValue)
                    {
                        var dyn = syn.InputsDynamic.Value;
                        string first = dyn.FirstInputType is { Count: > 0 }
                            ? "(" + string.Join("|", dyn.FirstInputType.Select(TypeNameDisplay.GetDisplayTypeName)) + ")"
                            : "-";
                        string middle = dyn.MiddleInputTypes is { Count: > 0 }
                            ? "(" + string.Join("|", dyn.MiddleInputTypes.Select(TypeNameDisplay.GetDisplayTypeName)) + ")"
                            : "-";
                        string last = dyn.LastInputType is { Count: > 0 }
                            ? "(" + string.Join("|", dyn.LastInputType.Select(TypeNameDisplay.GetDisplayTypeName)) + ")"
                            : "-";
                        return $"  {scenarioPart}Dynamic: first={first}, middle={middle}* (min {dyn.MinMiddleArgumentsCount}), last={last} -> {TypeNameDisplay.GetDisplayTypeName(syn.OutputType)}";
                    }
                    else
                    {
                        return $"  {scenarioPart}Empty -> {TypeNameDisplay.GetDisplayTypeName(syn.OutputType)}";
                    }
                });

                string message =
                    $"'{Name}' arguments do not match any declared syntax." +
                    $"{Environment.NewLine}Provided types: [{resolvedNames}]" +
                    $"{Environment.NewLine}Available syntaxes:{Environment.NewLine}{syntaxesDescription}";

                return ValidationHelpers.FailureResult("arguments", message, resolvedNames);
            });
    }

    public Result<Type[], ValidationResult> ValidateArgumentTypesLegacy(object?[] args, bool allowParentTypes = true) => //to be removed later
          ValidateArgumentTypes(args, allowParentTypes)
          .Match<Result<Type[], ValidationResult>>(
              ok => ok.ResolvedTypes,
              err => err
          );


    public ValidationResult ValidateStringConstraints(object?[] callArgs)
    {
        for (int i = 0; i < callArgs.Length; i++)
        {
            if (callArgs[i] is not string strArg) continue;

            // Values
            HashSet<string>? allowedValues = null;
            if (AllowedStringValuesForLast is { Count: > 0 } && i == callArgs.Length - 1)
                allowedValues = AllowedStringValuesForLast;
            else if (AllowedStringValuesPerPosition is not null && AllowedStringValuesPerPosition.TryGetValue(i, out var set) && set.Count > 0)
                allowedValues = set;
            else if (AllowedStringValuesForAll is { Count: > 0 })
                allowedValues = AllowedStringValuesForAll;

            if (allowedValues is not null && allowedValues.Count > 0)
            {
                if (!allowedValues.Contains(strArg, StringComparer.OrdinalIgnoreCase))
                {
                    string posText = ValidationHelpers.ToOrdinal(i + 1);
                    return ValidationHelpers.FailureResult(
                        "arguments",
                        $"{Name} function allowed string values for the {posText} argument are [{string.Join(", ", allowedValues)}], got '{strArg}'.",
                        strArg);
                }
            }

            // Formats (regex)
            HashSet<string>? allowedFormats = null;
            if (AllowedStringFormatsForLast is { Count: > 0 } && i == callArgs.Length - 1)
                allowedFormats = AllowedStringFormatsForLast;
            else if (AllowedStringFormatsPerPosition is not null && AllowedStringFormatsPerPosition.TryGetValue(i, out var fmtSet) && fmtSet.Count > 0)
                allowedFormats = fmtSet;
            else if (AllowedStringFormatsForAll is { Count: > 0 })
                allowedFormats = AllowedStringFormatsForAll;

            if (allowedFormats is not null && allowedFormats.Count > 0)
            {
                bool matches = allowedFormats.Any(fmt =>
                    !string.IsNullOrEmpty(fmt) &&
                    Regex.IsMatch(strArg, fmt, RegexOptions.IgnoreCase));
                if (!matches)
                {
                    string posText = ValidationHelpers.ToOrdinal(i + 1);
                    return ValidationHelpers.FailureResult(
                        "arguments",
                        $"{Name} function allowed string formats for the {posText} argument are [{string.Join(", ", allowedFormats)}], got '{strArg}'.",
                        strArg);
                }
            }
        }
        return new ValidationResult();
    }

    public FunctionDefinitionDto ToDefinitionDto() =>
        FunctionDefinitionDto.From(this);
}


