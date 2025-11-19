using CustomResultError;
using FluentValidation.Results;
using ParserLibrary.Parsers;
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
    public Func<object?,object?, Result<BinaryOperatorSyntaxMatch, ValidationResult>>? AdditionalGlobalValidation { get; init; }

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

    public ValidationResult Validate(object? left, object? right, bool allowParentTypes = true)
        => GetValidSyntax(left, right, allowParentTypes).Match(_ => ValidationHelpers.Success, err => err);

    public Result<BinaryOperatorSyntaxMatch, ValidationResult> GetValidSyntax(object? left, object? right, bool allowParentTypes = true)
    {
        if (Syntaxes is null || Syntaxes.Count == 0)
            return ValidationHelpers.FailureResult("operator", $"Operator '{Name}' has no declared syntaxes.", null);

        // Resolve types (support passing Type directly)
        var leftType = GetArgumentType(left);
        var rightType = GetArgumentType(right);

        foreach (var syn in Syntaxes)
        {
            if (!syn.IsMatch(leftType, rightType, allowParentTypes)) continue;

            if (syn.AdditionalValidation is not null)
            {
                var v = syn.AdditionalValidation(left, right);
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
                var g = AdditionalGlobalValidation(left, right);
                if (g.IsFailure) return g.Error!;
            }

            return match;
        }

        return ValidationHelpers.FailureResult("operands", $"{Name} operator operands do not match any declared syntax.", null);
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