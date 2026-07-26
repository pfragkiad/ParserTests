using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;
using System.Linq;

namespace ParserLibrary.Definitions;

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
    public Func<object?[], Result<UnaryOperatorSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }
    public Func<object?[], object?, CancellationToken, Task<Result<UnaryOperatorSyntaxMatch, ValidationResult>>>? AdditionalGlobalValidationAsync { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object? operand, bool allowParentTypes = true)
    {
        var res = GetValidSyntax(operand, allowParentTypes);
        if (res.IsFailure) return res.Error!;
        return res.Value!.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(object? operand, object? context = null, bool allowParentTypes = true)
    {
        var match = GetValidSyntax(operand, allowParentTypes);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.Calc is null)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc).", null);

        return syn.Calc([operand], context);
    }

    public async Task<Result<object?, ValidationResult>> ValidateAndCalcAsync(object? operand, object? context = null, bool allowParentTypes = true, CancellationToken ct = default)
    {
        var match = await GetValidSyntaxAsync(operand, context, allowParentTypes, ct);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.CalcAsync is not null)
            return await syn.CalcAsync([operand], context, ct);
        if (syn.Calc is not null)
            return syn.Calc([operand], context);

        return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' is not executable (no Calc/CalcAsync).", null);
    }

    public ValidationResult Validate(object? operand, bool allowParentTypes = true)
        => GetValidSyntax(operand, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<UnaryOperatorSyntaxMatch, ValidationResult> GetValidSyntax(object? operand, bool allowParentTypes = true)
    {
        var operandType = GetArgumentType(operand);

        var syntaxResult = FindMatchingSyntax(
            Syntaxes,
            syn => syn.IsMatch(operandType, allowParentTypes),
            syn => syn.AdditionalValidation?.Invoke([operand]) ?? ValidationHelpers.Success,
            noSyntaxCategory: "operator",
            noSyntaxMessage: $"Operator '{Name}' has no declared syntaxes.",
            noMatchValidationFactory: () =>
            {
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
            });

        if (syntaxResult.IsFailure) return syntaxResult.Error!;

        if (AdditionalGlobalValidation is not null)
        {
            var globalValidation = AdditionalGlobalValidation([operand]);
            if (globalValidation.IsFailure) return globalValidation.Error!;
        }

        return new UnaryOperatorSyntaxMatch
        {
            MatchedSyntax = syntaxResult.Value!,
            OperandType = operandType
        };
    }

    public async Task<Result<UnaryOperatorSyntaxMatch, ValidationResult>> GetValidSyntaxAsync(object? operand, object? context = null, bool allowParentTypes = true, CancellationToken ct = default)
    {
        var operandType = GetArgumentType(operand);

        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' has no declared syntaxes.", null);

        foreach (var syn in Syntaxes)
        {
            if (!syn.IsMatch(operandType, allowParentTypes)) continue;

            if (syn.AdditionalValidationAsync is not null)
            {
                var validation = await syn.AdditionalValidationAsync([operand], context, ct);
                if (!validation.IsValid) return validation;
            }
            else
            {
                var validation = syn.AdditionalValidation?.Invoke([operand]) ?? ValidationHelpers.Success;
                if (!validation.IsValid) return validation;
            }

            if (AdditionalGlobalValidationAsync is not null)
            {
                var globalValidation = await AdditionalGlobalValidationAsync([operand], context, ct);
                if (globalValidation.IsFailure) return globalValidation.Error!;
            }
            else if (AdditionalGlobalValidation is not null)
            {
                var globalValidation = AdditionalGlobalValidation([operand]);
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
}