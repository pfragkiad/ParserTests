using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;

namespace ParserLibrary.Definitions.Functions;



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


    //args, context,  return ValidationResult (success or failure with message)
    public Func<object?[], object?, Result<FunctionSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }

    //args, context, cancellation token, return ValidationResult (success or failure with message)
    public Func<object?[], object?, CancellationToken, Task<Result<FunctionSyntaxMatch, ValidationResult>>>? AdditionalGlobalValidationAsync { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object?[] args, ParserContext? context, bool allowParentTypes)
    {
        var result = ValidateArgumentTypes(args, context, allowParentTypes);
        if (result.IsFailure) return result.Error!;
        var syntaxMatch = result.Value!;
        return syntaxMatch.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(object?[] args, ParserContext? context, bool allowParentTypes) //only Calc, no async
    {
        var syntaxMatch = ValidateArgumentTypes(args, context, allowParentTypes);
        if (syntaxMatch.IsFailure) return syntaxMatch.Error!;

        var syntax = syntaxMatch.Value!.MatchedSyntax;
        if (syntax.Calc is not null)
            return syntax.Calc(args, context);
        if (ParserLibrarySettings.WithCalcFallback && syntax.CalcAsync is not null)
            return syntax.CalcAsync(args, context, CancellationToken.None).GetAwaiter().GetResult();

        return ValidationHelpers.FailureResult("function", $"Function '{Name}' has no calculation method.", null);
    }

    //both CalcAsync and Calc support
    public async Task<Result<object?, ValidationResult>> ValidateAndCalcAsync(object?[] args, ParserContext? context, bool allowParentTypes, CancellationToken ct)
    {
        var syntaxMatch = await ValidateArgumentTypesAsync(args, context, allowParentTypes, ct);
        if (syntaxMatch.IsFailure) return syntaxMatch.Error!;

        var syntax = syntaxMatch.Value!.MatchedSyntax;
        if (syntax.CalcAsync is not null)
            return await syntax.CalcAsync(args, context, ct);
        if (ParserLibrarySettings.WithCalcFallback && syntax.Calc is not null)
            return syntax.Calc(args, context);

        return ValidationHelpers.FailureResult("function", $"Function '{Name}' has no calculation method.", null);
    }



    public ValidationResult Validate(object?[] args, ParserContext? context, bool allowParentTypes)
    {
        var result = ValidateArgumentTypes(args, context, allowParentTypes);
        return result.Match(_ => ValidationHelpers.Success, err => err);
    }

    // Reuse GetValidSyntax internally; Apply AdditionalValidation and return resolved types + matched syntax
    public Result<FunctionSyntaxMatch, ValidationResult> ValidateArgumentTypes(object?[] args,ParserContext? context,  bool allowParentTypes)
    {
        // Use the single source of truth for matching and string constraints
        var syntaxResult = GetValidSyntax(args, context, allowParentTypes);
        if (syntaxResult.IsFailure) return syntaxResult.Error!;

        // Additional business validation after syntax and string checks
        if (AdditionalGlobalValidation is not null)
        {
            var addVal = AdditionalGlobalValidation(args, context);
            if (addVal.IsFailure) return addVal.Error!;
        }
        else if (ParserLibrarySettings.WithValidationFallback && AdditionalGlobalValidationAsync is not null)
        {
            var addVal = AdditionalGlobalValidationAsync(args, context, CancellationToken.None).GetAwaiter().GetResult();
            if (addVal.IsFailure) return addVal.Error!;
        }

        // Resolve argument types (support passing Type directly) for the return payload
        var resolved = ResolveArgumentTypes(args);

        return new FunctionSyntaxMatch
        {
            MatchedSyntax = syntaxResult.Value!,
            ResolvedTypes = resolved
        };
    }

    // Reuse GetValidSyntaxAsync internally; Apply AdditionalValidationAsync and return resolved types + matched syntax
    public async Task<Result<FunctionSyntaxMatch, ValidationResult>> ValidateArgumentTypesAsync(object?[] args, ParserContext? context, bool allowParentTypes, CancellationToken ct)
    {
        var syntaxResult = await GetValidSyntaxAsync(args, context, allowParentTypes, ct);
        if (syntaxResult.IsFailure) return syntaxResult.Error!;

        if (AdditionalGlobalValidationAsync is not null)
        {
            var addVal = await AdditionalGlobalValidationAsync(args, context, ct);
            if (addVal.IsFailure) return addVal.Error!;
        }
        else if (ParserLibrarySettings.WithValidationFallback && AdditionalGlobalValidation is not null)
        {
            var addVal = AdditionalGlobalValidation(args, context);
            if (addVal.IsFailure) return addVal.Error!;
        }

        var resolved = ResolveArgumentTypes(args);

        return new FunctionSyntaxMatch
        {
            MatchedSyntax = syntaxResult.Value!,
            ResolvedTypes = resolved
        };
    }

    // Centralized matcher: validates syntaxes, nulls, type compatibility (with inheritance), and string constraints
    public Result<FunctionSyntax, ValidationResult> GetValidSyntax(object?[] args, ParserContext? context, bool allowParentTypes)
        => GetValidSyntaxAsync(args, context, allowParentTypes, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<Result<FunctionSyntax, ValidationResult>> GetValidSyntaxAsync(object?[] args, ParserContext? context, bool allowParentTypes, CancellationToken ct)
    {
        var resolved = ResolveArgumentTypes(args);

        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.FailureResult("function", $"Function '{Name}' has no declared syntaxes.", null);

        foreach (var syn in Syntaxes)
        {
            if (!(syn.IsFixedMatch(resolved, allowParentTypes) || syn.IsDynamicMatch(resolved, allowParentTypes)))
                continue;

            var strCheck = ValidateStringConstraints(args);
            if (!strCheck.IsValid) return strCheck;

            if (syn.AdditionalValidationAsync is not null)
            {
                var addValAsync = await syn.AdditionalValidationAsync(args, context, ct);
                if (!addValAsync.IsValid) return addValAsync;
            }
            else if (syn.AdditionalValidation is not null)
            {
                var addVal = syn.AdditionalValidation(args, context);
                if (!addVal.IsValid) return addVal;
            }

            return syn;
        }

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
    }

    //public Result<Type[], ValidationResult> ValidateArgumentTypesLegacy(object?[] args, bool allowParentTypes = true) => //to be removed later
    //      ValidateArgumentTypes(args, allowParentTypes)
    //      .Match<Result<Type[], ValidationResult>>(
    //          ok => ok.ResolvedTypes,
    //          err => err
    //      );


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

    public FunctionDefinitionDto ToDefinitionDto() 
    {
        return new FunctionDefinitionDto
        {
            Name = Name,
            IsCustomFunction = IsCustomFunction,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description,

            Aliases = Aliases is { Length: > 0 }
                ? [.. Aliases.Distinct()]
                : null,

            //MinArgumentsCount = MinArgumentsCount,
            //MaxArgumentsCount = MaxArgumentsCount,
            //FixedArgumentsCount = FixedArgumentsCount,

            Examples = Examples?.Count > 0 ? [.. Examples] : null,

            //// Types (names instead of Type)
            //AllowedTypesPerPosition = AllowedTypesPerPosition is { Count: > 0 }
            //    ? [.. AllowedTypesPerPosition
            //        .Select((set, idx) => set is { Count: > 0 }
            //            ? new AllowedTypesPerPositionDto
            //            {
            //                Position = idx + 1, // 1-based
            //                Types = set.Select(TypeNameDisplay.GetDisplayTypeName)
            //                           .Distinct()
            //                           .ToList()
            //            }
            //            : null)
            //        .Where(x => x is not null)
            //        .Select(x => x!)]
            //    : null,

            //AllowedTypesForAll = AllowedTypesForAll is { Count: > 0 }
            //    ? [.. AllowedTypesForAll.Select(TypeNameDisplay.GetDisplayTypeName)]
            //    : null,

            //AllowedTypesForLast = AllowedTypesForLast is { Count: > 0 }
            //    ? [.. AllowedTypesForLast.Select(TypeNameDisplay.GetDisplayTypeName)]
            //    : null,

            // String values per position
            AllowedStringValuesPerPosition = AllowedStringValuesPerPosition is { Count: > 0 }
                ? [.. AllowedStringValuesPerPosition
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value is { Count: > 0 }
                        ? new ValuesPerPositionDto
                        {
                            Position = kv.Key + 1, // 1-based
                            Values = kv.Value.ToList()
                        }
                        : null)
                    .Where(x => x is not null)
                    .Select(x => x!)]
                : null,

            AllowedStringValuesForAll = AllowedStringValuesForAll is { Count: > 0 }
                ? [.. AllowedStringValuesForAll]
                : null,

            AllowedStringValuesForLast = AllowedStringValuesForLast is { Count: > 0 }
                ? [.. AllowedStringValuesForLast]
                : null,

            // String formats per position
            AllowedStringFormatsPerPosition = AllowedStringFormatsPerPosition is { Count: > 0 }
                ? [.. AllowedStringFormatsPerPosition
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value is { Count: > 0 }
                        ? new ValuesPerPositionDto
                        {
                            Position = kv.Key + 1, // 1-based
                            Values = kv.Value.ToList()
                        }
                        : null)
                    .Where(x => x is not null)
                    .Select(x => x!)]
                : null,

            AllowedStringFormatsForAll = AllowedStringFormatsForAll is { Count: > 0 }
                ? [.. AllowedStringFormatsForAll]
                : null,

            AllowedStringFormatsForLast = AllowedStringFormatsForLast is { Count: > 0 }
                ? [.. AllowedStringFormatsForLast]
                : null,

            // Function syntaxes
            Syntaxes = Syntaxes is { Count: > 0 }
                ? [.. Syntaxes.Select(MapSyntax)]
                : null
        };
    }

    private static FunctionSyntaxDto MapSyntax(FunctionSyntax syn)
    {
        var dyn = syn.InputsDynamic;

        // Map to [{ position, types[] }] with 1-based positions
        var inputsFixed = syn.InputsFixed is { Count: > 0 }
            ? syn.InputsFixed
                .Select((set, idx) => set is { Count: > 0 }
                    ? new InputFixedDto
                    {
                        Position = idx + 1,
                        Types = set.Select(TypeNameDisplay.GetDisplayTypeName).Distinct().ToList()
                    }
                    : null)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList()
            : null;

        InputsDynamicDto? inputsDynamic = null;
        if (dyn is not null)
        {
            var first = dyn.Value.FirstInputType;
            var last = dyn.Value.LastInputType;
            var middle = dyn.Value.MiddleInputTypes;
            var minVar = dyn.Value.MinMiddleArgumentsCount;

            var hasFirst = first is { Count: > 0 };
            var hasLast = last is { Count: > 0 };
            var hasMiddle = middle is { Count: > 0 };

            if (hasFirst || hasLast || hasMiddle || minVar > 0)
            {
                inputsDynamic = new InputsDynamicDto
                {
                    FirstInputTypes = hasFirst ? [.. first!.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()] : null,
                    LastInputTypes = hasLast ? [.. last!.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()] : null,
                    Types = hasMiddle ? [.. middle!.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()] : null,
                    MinVariableArgumentsCount = minVar > 0 ? minVar : null
                };
            }
        }

        return new FunctionSyntaxDto
        {
            Scenario = syn.Scenario,
            Expression = string.IsNullOrWhiteSpace(syn.Expression) ? null : syn.Expression,
            ExpressionClean = syn.ExpressionClean,
            InputsFixed = inputsFixed,
            InputsDynamic = inputsDynamic,
            // multi-examples array
            Examples = syn.Examples is { Length: > 0 } ? [.. syn.Examples] : null,
            // ensure output last via JsonPropertyOrder
            OutputType = syn.OutputType is not null
                ? TypeNameDisplay.GetDisplayTypeName(syn.OutputType)
                : null,
            Description = string.IsNullOrWhiteSpace(syn.Description) ? null : syn.Description
        };
    }
}
