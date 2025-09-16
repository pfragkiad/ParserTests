using FluentValidation.Results;

namespace ParserLibrary.Parsers.Validation.CheckResults;

public readonly struct OperatorArgumentCheckResult
{
    public string Operator { get; init; }

    public int Position { get; init; }

}

public class InvalidOperatorsCheckResult : CheckResult
{
    public virtual string Category { get; } = "";

    public List<OperatorArgumentCheckResult> InvalidOperators { get; init; } = [];

    public List<OperatorArgumentCheckResult> ValidOperators { get; init; } = [];

    public override bool IsSuccess => base.IsSuccess && InvalidOperators.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        if (IsSuccess) return [];

        string propertyName = $"Formula.{Category}OperatorOperands";
        return [
            .. base.GetValidationFailures(),
            .. InvalidOperators.Select(op => new ValidationFailure(propertyName, $"Invalid operator '{op.Operator}' operands at position {op.Position}."))
        ];
    }
}

public class InvalidBinaryOperatorsCheckResult : InvalidOperatorsCheckResult
{
    public override string Category => "Binary";
}

public class InvalidUnaryOperatorsCheckResult : InvalidOperatorsCheckResult
{
    public override string Category => "Unary";

}