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
    public Func<object?, object?, Result<BinaryOperatorSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object? left, object? right, bool allowParentTypes = true)
    {
        var res = GetValidSyntax(left, right, allowParentTypes);
        if (res.IsFailure) return res.Error!;
        return res.Value!.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(object? left, object?
        right, object? context = null, bool allowParentTypes = true)
    {
        var match = GetValidSyntax(left, right, allowParentTypes);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.Calc is null)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc).", null);

        return syn.Calc(left, right, context);
    }

    public async Task<Result<object?, ValidationResult>> ValidateAndCalcAsync(object? left, object? right, object? context = null, bool allowParentTypes = true, CancellationToken ct = default)
    {
        var match = GetValidSyntax(left, right, allowParentTypes);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.CalcAsync is not null)
            return await syn.CalcAsync(left, right, context, ct);
        if (syn.Calc is not null)
            return syn.Calc(left, right, context);

        return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc/CalcAsync).", null);
    }

    public ValidationResult Validate(object? left, object? right, bool allowParentTypes = true)
        => GetValidSyntax(left, right, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<BinaryOperatorSyntaxMatch, ValidationResult> GetValidSyntax(object? left, object? right, bool allowParentTypes = true)
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
            var globalValidation = AdditionalGlobalValidation(left, right);
            if (globalValidation.IsFailure) return globalValidation.Error!;
        }

        return new BinaryOperatorSyntaxMatch
        {
            MatchedSyntax = syntaxResult.Value!,
            LeftType = leftType,
            RightType = rightType
        };
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