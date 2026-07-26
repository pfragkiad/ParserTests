using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Definitions;

public sealed class BinaryOperatorDefinition : OperatorDefinition
{
    // Allowed operand type pairs. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<(Type Left, Type Right)>? AllowedTypePairs { get; init; }

    // Alternative binary forms (e.g., (int,int)->int, (double,double)->double, (Item,int)->Item, etc.)
    public List<BinaryOperatorSyntax>? Syntaxes { get; init; }

    // Optional cross-syntax validation (e.g., domain rules)
    public Func<object?,object?, ParserContext?, Result<BinaryOperatorSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }
    public Func<object?, object?, ParserContext?, CancellationToken, Task<Result<BinaryOperatorSyntaxMatch, ValidationResult>>>? AdditionalGlobalValidationAsync { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object? left, object? right,ParserContext? context,  bool allowParentTypes)
    {
        var res = GetValidSyntax(left, right, context, allowParentTypes);
        if (res.IsFailure) return res.Error!;
        return res.Value!.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(
        object? left,
        object? right,
        ParserContext? context,
        bool allowParentTypes)
    {
        var match = GetValidSyntax(left, right, context, allowParentTypes);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.Calc is not null)
            return syn.Calc(left, right, context);
        //if (syn.CalcAsync is not null) //temp disable
        //    return syn.CalcAsync(left, right, context, CancellationToken.None).GetAwaiter().GetResult();

        return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc/CalcAsync).", null);
    }

    public async Task<Result<object?, ValidationResult>> ValidateAndCalcAsync(
        object? left,
        object? right,
        ParserContext? context,
        bool allowParentTypes,
        CancellationToken ct)
    {
        var match = await GetValidSyntaxAsync(left, right, context, allowParentTypes, ct);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.CalcAsync is not null)
            return await syn.CalcAsync(left, right, context, ct);
        if (syn.Calc is not null)
            return syn.Calc(left, right, context);

        return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc/CalcAsync).", null);
    }

    public ValidationResult Validate(object? left, object? right, ParserContext? context, bool allowParentTypes)
        => GetValidSyntax(left, right, context, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<BinaryOperatorSyntaxMatch, ValidationResult> GetValidSyntax(object? left, object? right,ParserContext? context,  bool allowParentTypes)
    {
        var leftType = GetArgumentType(left);
        var rightType = GetArgumentType(right);

        var syntaxResult = FindMatchingSyntax(
            Syntaxes,
            syn => syn.IsMatch(leftType, rightType, allowParentTypes),
            syn => syn.AdditionalValidation?.Invoke(left, right) ?? ValidationHelpers.Success,
            noSyntaxCategory: "operator",
            noSyntaxMessage: $"Operator '{Name}' has no declared syntaxes.",
            noMatchValidationFactory: () =>
            {
                var resolvedNames = $"({FormatTypeName(leftType)}, {FormatTypeName(rightType)})";

                string syntaxesDescription = BuildSyntaxesDescription(Syntaxes, syn =>
                {
                    string scenarioPart = syn.Scenario.HasValue ? $"(Scenario {syn.Scenario}) " : "";
                    var left = FormatTypeSet(syn.LeftTypes);
                    var right = FormatTypeSet(syn.RightTypes);
                    return $"  {scenarioPart}Binary: ({left}, {right}) -> {FormatTypeName(syn.OutputType)}";
                });

                string message =
                    $"'{Name}' operator operands do not match any declared syntax." +
                    $"{Environment.NewLine}Provided types: [{resolvedNames}]" +
                    $"{Environment.NewLine}Available syntaxes:{Environment.NewLine}{syntaxesDescription}";

                return ValidationHelpers.FailureResult("operands", message, resolvedNames);
            });

        if (syntaxResult.IsFailure) return syntaxResult.Error!;

        if (AdditionalGlobalValidation is not null)
        {
            var globalValidation = AdditionalGlobalValidation(left, right, context);
            if (globalValidation.IsFailure) return globalValidation.Error!;
        }

        return new BinaryOperatorSyntaxMatch
        {
            MatchedSyntax = syntaxResult.Value!,
            LeftType = leftType,
            RightType = rightType
        };
    }

    public async Task<Result<BinaryOperatorSyntaxMatch, ValidationResult>> GetValidSyntaxAsync(
        object? left,
        object? right,
        ParserContext? context,
        bool allowParentTypes,
        CancellationToken ct)
    {
        var leftType = GetArgumentType(left);
        var rightType = GetArgumentType(right);

        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' has no declared syntaxes.", null);

        foreach (var syn in Syntaxes)
        {
            if (!syn.IsMatch(leftType, rightType, allowParentTypes)) continue;

            if (syn.AdditionalValidationAsync is not null)
            {
                var validation = await syn.AdditionalValidationAsync(left, right, context, ct);
                if (!validation.IsValid) return validation;
            }
            else
            {
                var validation = syn.AdditionalValidation?.Invoke(left, right) ?? ValidationHelpers.Success;
                if (!validation.IsValid) return validation;
            }

            if (AdditionalGlobalValidationAsync is not null)
            {
                var globalValidation = await AdditionalGlobalValidationAsync(left, right, context, ct);
                if (globalValidation.IsFailure) return globalValidation.Error!;
            }
            else if (AdditionalGlobalValidation is not null)
            {
                var globalValidation = AdditionalGlobalValidation(left, right, context);
                if (globalValidation.IsFailure) return globalValidation.Error!;
            }

            return new BinaryOperatorSyntaxMatch
            {
                MatchedSyntax = syn,
                LeftType = leftType,
                RightType = rightType
            };
        }

        var resolvedNames = $"({FormatTypeName(leftType)}, {FormatTypeName(rightType)})";

        string syntaxesDescription = BuildSyntaxesDescription(Syntaxes, syn =>
        {
            string scenarioPart = syn.Scenario.HasValue ? $"(Scenario {syn.Scenario}) " : "";
            var leftTypes = FormatTypeSet(syn.LeftTypes);
            var rightTypes = FormatTypeSet(syn.RightTypes);
            return $"  {scenarioPart}Binary: ({leftTypes}, {rightTypes}) -> {FormatTypeName(syn.OutputType)}";
        });

        string message =
            $"'{Name}' operator operands do not match any declared syntax." +
            $"{Environment.NewLine}Provided types: [{resolvedNames}]" +
            $"{Environment.NewLine}Available syntaxes:{Environment.NewLine}{syntaxesDescription}";

        return ValidationHelpers.FailureResult("operands", message, resolvedNames);
    }

    // New: instance mapper to DTO (mirrors FunctionInformation pattern)
    public BinaryOperatorDefinitionDto ToDefinitionDto()
    {
        return new BinaryOperatorDefinitionDto
        {
            Name = Name,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
            Aliases = Aliases is { Length: > 0 } ? [.. Aliases.Distinct()] : null,
            Examples = Examples is { Count: > 0 }
                ? [.. Examples.Select(e => new SyntaxExampleDto
                {
                    Syntax = e.Syntax,
                    Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description
                })]
                : null,
            Syntaxes = Syntaxes is { Count: > 0 } ? [.. Syntaxes.Select(MapSyntax)] : null
        };
    }

    private static BinaryOperatorSyntaxDto MapSyntax(BinaryOperatorSyntax syn)
    {
        return new BinaryOperatorSyntaxDto
        {
            Scenario = syn.Scenario,
            LeftTypes = syn.LeftTypes is { Count: > 0 }
                ? [.. syn.LeftTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()]
                : null,
            RightTypes = syn.RightTypes is { Count: > 0 }
                ? [.. syn.RightTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()]
                : null,
            Examples = syn.Examples is { Length: > 0 } ? syn.Examples.Distinct().ToList() : null,
            OutputType = syn.OutputType is not null ? TypeNameDisplay.GetDisplayTypeName(syn.OutputType) : null,
            Description = string.IsNullOrWhiteSpace(syn.Description) ? null : syn.Description
        };
    }
}