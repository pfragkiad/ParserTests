using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers.Helpers;
using System.Text.Json.Serialization;

namespace ParserLibrary.Meta;

public sealed class BinaryOperatorInformation : OperatorInformation
{
    // Allowed operand type pairs. Ignored in JSON; the converter emits display type names.
    [JsonIgnore]
    public IReadOnlyList<(Type Left, Type Right)>? AllowedTypePairs { get; init; }

    // Alternative binary forms (e.g., (int,int)->int, (double,double)->double, (Item,int)->Item, etc.)
    public List<BinaryOperatorSyntax>? Syntaxes { get; init; }

    // Optional cross-syntax validation (e.g., domain rules)
    public Func<object?[], Result<BinaryOperatorSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }

    public Result<Type, ValidationResult> ResolveOutputType(object? left, object? right, bool allowParentTypes = true)
    {
        var res = ValidateOperands(left, right, allowParentTypes);
        if (res.IsFailure) return res.Error!;
        return res.Value!.MatchedSyntax.OutputType;
    }

    public Result<object?, ValidationResult> ValidateAndCalc(object? left, object?
        right, object? context = null, bool allowParentTypes = true)
    {
        var match = ValidateOperands(left, right, allowParentTypes);
        if (match.IsFailure) return match.Error!;

        var syn = match.Value!.MatchedSyntax;
        if (syn.Calc is null)
            return ValidationHelpers.GetFailureResult("operator", $"Operator '{Name}' is not executable (no Calc).", null);

        return syn.Calc([left, right], context);
    }

    public ValidationResult Validate(object? left, object? right, bool allowParentTypes = true)
        => ValidateOperands(left, right, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<BinaryOperatorSyntaxMatch, ValidationResult> ValidateOperands(object? left, object? right, bool allowParentTypes = true)
    {
        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.GetFailureResult("operator", $"Operator '{Name}' has no declared syntaxes.", null);

        // Nulls not allowed
        if (left is null || right is null)
            return ValidationHelpers.GetFailureResult("operands", $"{Name} operator does not accept null operands.", null);

        // Resolve types (support passing Type directly)
        var leftType = left is Type lt ? lt : left.GetType();
        var rightType = right is Type rt ? rt : right.GetType();

        foreach (var syn in Syntaxes)
        {
            if (!syn.IsMatch(leftType, rightType, allowParentTypes)) continue;

            if (syn.AdditionalValidation is not null)
            {
                var v = syn.AdditionalValidation([left, right]);
                if (!v.IsValid) return v;
            }

            var match = new BinaryOperatorSyntaxMatch
            {
                MatchedSyntax = syn,
                LeftType = leftType,
                RightType = rightType
            };

            if (AdditionalGlobalValidation is not null)
            {
                var g = AdditionalGlobalValidation([left, right]);
                if (g.IsFailure) return g.Error!;
            }

            return match;
        }

        return ValidationHelpers.GetFailureResult("operands", $"{Name} operator operands do not match any declared syntax.", null);
    }
}