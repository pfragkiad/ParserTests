using FluentValidation.Results;

namespace ParserLibrary.Tokenizers.CheckResults;

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

    public override bool IsSuccess => InvalidOperators.Count == 0;

    public override IList<ValidationFailure> GetValidationFailures()
    {
        return [.. InvalidOperators.Select(op => new ValidationFailure("Formula", $"Invalid operator '{op.Operator}' operands at position {op.Position}."))];
    }
}

public class InvalidBinaryOperatorsCheckResult : InvalidOperatorsCheckResult
{
    public override string Category => "binary";
}

public class InvalidUnaryOperatorsCheckResult : InvalidOperatorsCheckResult
{
    public override string Category => "unary";

}