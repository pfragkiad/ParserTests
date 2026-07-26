using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;
using System.Linq;

namespace ParserLibrary.Definitions.UnaryOperators;

public sealed class UnaryOperatorDefinition : OperatorDefinition
{
    // Unary placement kind
    public UnaryOperatorKind Kind { get; init; }

    // Allowed single-operand types. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<Type>? AllowedOperandTypes { get; init; }

    // Alternative unary forms, e.g., '-' for int/double, '!' for bool, '~' for int
    public List<UnaryOperatorSyntax>? Syntaxes { get; init; }

    // Optional cross-syntax validation (e.g., business rules)
    public Func<object?, ParserContext?, Result<UnaryOperatorSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }
    public Func<object?, ParserContext?, CancellationToken, Task<Result<UnaryOperatorSyntaxMatch, ValidationResult>>>? AdditionalGlobalValidationAsync { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object? operand, ParserContext? context, bool allowParentTypes)
    {
        var res = GetValidSyntax(operand, context, allowParentTypes);
        if (res.IsFailure) return res.Error!;
        return res.Value!.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(object? operand, ParserContext? context, bool allowParentTypes)
    {
        var match = GetValidSyntax(operand, context, allowParentTypes);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.Calc is not null)
            return syn.Calc(operand, context);
        if (ParserLibrarySettings.WithCalcFallback && syn.CalcAsync is not null)
            return syn.CalcAsync(operand, context, CancellationToken.None).GetAwaiter().GetResult();

        return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc/CalcAsync).", null);
    }

    public async Task<Result<object?, ValidationResult>> ValidateAndCalcAsync(object? operand, ParserContext? context, bool allowParentTypes, CancellationToken ct)
    {
        var match = await GetValidSyntaxAsync(operand, context, allowParentTypes, ct);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.CalcAsync is not null)
            return await syn.CalcAsync(operand, context, ct);
        if (ParserLibrarySettings.WithCalcFallback && syn.Calc is not null)
            return syn.Calc(operand, context);

        return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc/CalcAsync).", null);
    }

    public ValidationResult Validate(object? operand, ParserContext? context, bool allowParentTypes)
        => GetValidSyntax(operand, context, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<UnaryOperatorSyntaxMatch, ValidationResult> GetValidSyntax(object? operand, ParserContext? context, bool allowParentTypes)
        => GetValidSyntaxAsync(operand, context, allowParentTypes, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<Result<UnaryOperatorSyntaxMatch, ValidationResult>> GetValidSyntaxAsync(object? operand, ParserContext? context, bool allowParentTypes, CancellationToken ct)
    {
        var operandType = GetArgumentType(operand);

        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' has no declared syntaxes.", null);

        foreach (var syn in Syntaxes)
        {
            if (!syn.IsMatch(operandType, allowParentTypes)) continue;

            if (syn.AdditionalValidationAsync is not null)
            {
                var validation = await syn.AdditionalValidationAsync(operand, context, ct);
                if (!validation.IsValid) return validation;
            }
            else
            {
                var validation = syn.AdditionalValidation?.Invoke(operand,context) ?? ValidationHelpers.Success;
                if (!validation.IsValid) return validation;
            }

            if (AdditionalGlobalValidationAsync is not null)
            {
                var globalValidation = await AdditionalGlobalValidationAsync(operand, context, ct);
                if (globalValidation.IsFailure) return globalValidation.Error!;
            }
            else if (ParserLibrarySettings.WithValidationFallback && AdditionalGlobalValidation is not null)
            {
                var globalValidation = AdditionalGlobalValidation(operand, context);
                if (globalValidation.IsFailure) return globalValidation.Error!;
            }

            return new UnaryOperatorSyntaxMatch
            {
                MatchedSyntax = syn,
                OperandType = operandType
            };
        }

        var resolvedNames = FormatTypeName(operandType);

        string syntaxesDescription = BuildSyntaxesDescription(Syntaxes, syn =>
        {
            string scenarioPart = syn.Scenario.HasValue ? $"(Scenario {syn.Scenario}) " : "";
            var operandTypes = FormatTypeSet(syn.OperandTypes);
            return $"  {scenarioPart}Unary: ({operandTypes}) -> {FormatTypeName(syn.OutputType)}";
        });

        string message =
            $"{Name} operator operand does not match any declared syntax." +
            $"{Environment.NewLine}Provided type: [{resolvedNames}]" +
            $"{Environment.NewLine}Available syntaxes:{Environment.NewLine}{syntaxesDescription}";

        return ValidationHelpers.FailureResult("operands", message, resolvedNames);
    }


    public UnaryOperatorDefinitionDto ToDefinitionDto()
    {
        return new UnaryOperatorDefinitionDto
        {
            Name = Name,
            Kind = Kind,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description,

            Aliases = Aliases is { Length: > 0 }
                ? [.. Aliases.Distinct()]
                : null,

            Examples = Examples?.Count > 0 ? [.. Examples] : null,

            Syntaxes = Syntaxes is { Count: > 0 }
                ? [.. Syntaxes.Select(MapSyntax)]
                : null
        };
    }

    private static UnaryOperatorSyntaxDto MapSyntax(UnaryOperatorSyntax syn)
    {
        return new UnaryOperatorSyntaxDto
        {
            Scenario = syn.Scenario,
            OperandTypes = syn.OperandTypes is { Count: > 0 }
                ? [.. syn.OperandTypes.Select(TypeNameDisplay.GetDisplayTypeName).Distinct()]
                : null,
            Examples = syn.Examples is { Length: > 0 } ? [.. syn.Examples] : null,
            // ensure output last via JsonPropertyOrder
            OutputType = syn.OutputType is not null
                ? TypeNameDisplay.GetDisplayTypeName(syn.OutputType)
                : null,
            Description = string.IsNullOrWhiteSpace(syn.Description) ? null : syn.Description
        };
    }
}