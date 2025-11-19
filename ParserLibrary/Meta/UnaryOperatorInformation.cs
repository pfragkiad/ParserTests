using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public enum UnaryOperatorKind : byte
{
    Prefix = 1,
    Postfix = 2
}

public sealed class UnaryOperatorInformation : OperatorInformation
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

    public ValidationResult Validate(object? operand, bool allowParentTypes = true)
        => GetValidSyntax(operand, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<UnaryOperatorSyntaxMatch, ValidationResult> GetValidSyntax(object? operand, bool allowParentTypes = true)
    {
        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' has no declared syntaxes.", null);

        var t = GetArgumentType(operand);

        foreach (var syn in Syntaxes)
        {
            if (!syn.IsMatch(t, allowParentTypes)) continue;

            if (syn.AdditionalValidation is not null)
            {
                var v = syn.AdditionalValidation([operand]);
                if (!v.IsValid) return v;
            }

            var match = new UnaryOperatorSyntaxMatch
            {
                MatchedSyntax = syn,
                OperandType = t
            };

            if (AdditionalGlobalValidation is not null)
            {
                var g = AdditionalGlobalValidation([operand]);
                if (g.IsFailure) return g.Error!;
            }

            return match;
        }

        return ValidationHelpers.FailureResult("operands", $"{Name} operator operand does not match any declared syntax.", null);
    }
}