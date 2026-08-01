using Microsoft.Extensions.Logging;
using ParserLibrary.Definitions;
using ParserLibrary.Definitions.BinaryOperators;
using ParserLibrary.Definitions.Functions;
using ParserLibrary.Definitions.UnaryOperators;
using ParserLibrary.Parsers;
using ParserLibrary.Parsers.Helpers;

namespace ParserTests.Common.Parsers;

public sealed class ValueAwareTypeInferenceParser : ParserBase
{
    public ValueAwareTypeInferenceParser(ILogger<ValueAwareTypeInferenceParser> logger, ParserServices ps) : base(logger, ps)
    {
        FunctionCatalog = new ValueAwareTypeInferenceFunctionCatalog();
        BinaryOperatorCatalog = new ValueAwareTypeInferenceBinaryOperatorCatalog();
        UnaryOperatorCatalog = new ValueAwareTypeInferenceUnaryOperatorCatalog();
    }

    protected override object EvaluateLiteral(string s, string? group)
        => int.TryParse(s, out var i)
            ? i
            : double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        var definition = GetFunctionInformation(functionName)
            ?? throw new InvalidOperationException($"Unknown function '{functionName}'.");

        var result = definition.ValidateAndCalc(args, Context, allowParentTypes: AllowParentTypesInValidation);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value;
    }

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        var result = ValidateAndEvaluateBinaryOperator(operatorName, leftOperand, rightOperand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value;
    }

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        var result = ValidateAndEvaluateUnaryOperator(operatorName, operand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value;
    }

    protected override Type EvaluateFunctionType(string functionName, object?[] args)
    {
        var result = ResolveFunctionType(functionName, args);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value!;
    }

    protected override Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand)
    {
        var result = ResolveBinaryOperatorType(operatorName, leftOperand, rightOperand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value!;
    }

    protected override Type EvaluateUnaryOperatorType(string operatorName, object? operand)
    {
        var result = ResolveUnaryOperatorType(operatorName, operand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value!;
    }
}

public sealed class ValueAwareTypeInferenceParserSession : ParserSessionBase
{
    public ValueAwareTypeInferenceParserSession(ILogger<ValueAwareTypeInferenceParserSession> logger, ParserServices ps) : base(logger, ps)
    {
        FunctionCatalog = new ValueAwareTypeInferenceFunctionCatalog();
        BinaryOperatorCatalog = new ValueAwareTypeInferenceBinaryOperatorCatalog();
        UnaryOperatorCatalog = new ValueAwareTypeInferenceUnaryOperatorCatalog();
    }

    protected override object EvaluateLiteral(string s, string? group)
        => int.TryParse(s, out var i)
            ? i
            : double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        var definition = GetFunctionInformation(functionName)
            ?? throw new InvalidOperationException($"Unknown function '{functionName}'.");

        var result = definition.ValidateAndCalc(args, Context, allowParentTypes: AllowParentTypesInValidation);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value;
    }

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        var result = ValidateAndEvaluateBinaryOperator(operatorName, leftOperand, rightOperand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value;
    }

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        var result = ValidateAndEvaluateUnaryOperator(operatorName, operand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value;
    }

    protected override Type EvaluateFunctionType(string functionName, object?[] args)
    {
        var result = ResolveFunctionType(functionName, args);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value!;
    }

    protected override Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand)
    {
        var result = ResolveBinaryOperatorType(operatorName, leftOperand, rightOperand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value!;
    }

    protected override Type EvaluateUnaryOperatorType(string operatorName, object? operand)
    {
        var result = ResolveUnaryOperatorType(operatorName, operand);
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error?.ToString());

        return result.Value!;
    }
}

public sealed class ValueAwareTypeInferenceFunctionCatalog : CatalogBase<FunctionDefinition>
{
    public override bool RefreshEachTime => false;

    public override bool IgnoreCase => true;

    public FunctionDefinition Pick => new()
    {
        Name = "pick",
        Syntaxes =
        [
            new FunctionSyntax
            {
                InputsFixed =
                [
                    [typeof(int), typeof(double)],
                    [typeof(int), typeof(double)],
                    [typeof(int), typeof(double)]
                ],
                OutputType = typeof(object),
                PossibleOutputTypes = [typeof(int), typeof(double)],
                ResolveOutputTypeFromValues = (args, _) =>
                {
                    if (args.Length == 0 || args[0] is not IConvertible c)
                        return null;

                    var selector = Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture);
                    var selectedIndex = selector == 2 ? 2 : 1;
                    return OperatorDefinition.GetArgumentType(args[selectedIndex]);
                },
                Calc = (args, _) =>
                {
                    var selector = args.Length > 0 && args[0] is IConvertible c
                        ? Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture)
                        : 1;
                    return selector == 2 ? args[2] : args[1];
                }
            }
        ]
    };

    public FunctionDefinition EchoAny => new()
    {
        Name = "echoany",
        Syntaxes =
        [
            new FunctionSyntax
            {
                InputsFixed =
                [
                    [typeof(AnyType)]
                ],
                OutputType = typeof(object),
                PossibleOutputTypes = [typeof(double), typeof(int), TypeHelpers.NullArgumentType],
                ResolveOutputTypeFromValues = (args, _) =>
                    args.Length == 0 ? null : OperatorDefinition.GetArgumentType(args[0]),
                Calc = (args, _) => args.Length == 0 ? null : args[0]
            }
        ]
    };

    public FunctionDefinition EchoAnyNonNull => new()
    {
        Name = "echoanynonnull",
        Syntaxes =
        [
            new FunctionSyntax
            {
                InputsFixed =
                [
                    [typeof(AnyNonNullType)]
                ],
                OutputType = typeof(object),
                PossibleOutputTypes = [typeof(double), typeof(int)],
                ResolveOutputTypeFromValues = (args, _) =>
                    args.Length == 0 || TypeHelpers.IsNullValue(args[0]) ? null : OperatorDefinition.GetArgumentType(args[0]),
                Calc = (args, _) => args.Length == 0 ? null : args[0]
            }
        ]
    };
}

public sealed class ValueAwareTypeInferenceBinaryOperatorCatalog : CatalogBase<BinaryOperatorDefinition>
{
    public override bool RefreshEachTime => false;

    public override bool IgnoreCase => false;

    public BinaryOperatorDefinition Plus => new()
    {
        Name = "+",
        Syntaxes =
        [
            new BinaryOperatorSyntax
            {
                LeftTypes = [typeof(int), typeof(double), TypeHelpers.NullArgumentType],
                RightTypes = [typeof(int), typeof(double), TypeHelpers.NullArgumentType],
                OutputType = typeof(object),
                PossibleOutputTypes = [typeof(int), typeof(double), TypeHelpers.NullArgumentType],
                ResolveOutputTypeFromValues = (left, right, _) =>
                {
                    if (TypeHelpers.IsNullValue(left) || TypeHelpers.IsNullValue(right))
                        return TypeHelpers.NullArgumentType;

                    if (left is not IConvertible c)
                        return null;

                    var selector = Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture);
                    return selector == 0 ? typeof(int) : OperatorDefinition.GetArgumentType(right);
                },
                Calc = (left, right, _) =>
                {
                    if (TypeHelpers.TryPropagateNullBinary(left, right, out var nullResult))
                        return nullResult;

                    var selector = left is IConvertible c
                        ? Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture)
                        : 0;
                    return selector == 0 ? 0 : right;
                }
            }
        ]
    };
}

public sealed class ValueAwareTypeInferenceUnaryOperatorCatalog : CatalogBase<UnaryOperatorDefinition>
{
    public override bool RefreshEachTime => false;

    public override bool IgnoreCase => false;

    public UnaryOperatorDefinition Minus => new()
    {
        Name = "-",
        Kind = UnaryOperatorKind.Prefix,
        Syntaxes =
        [
            new UnaryOperatorSyntax
            {
                OperandTypes = [typeof(int), typeof(double), TypeHelpers.NullArgumentType],
                OutputType = typeof(object),
                PossibleOutputTypes = [typeof(int), typeof(double), TypeHelpers.NullArgumentType],
                ResolveOutputTypeFromValues = (operand, _) =>
                {
                    if (TypeHelpers.IsNullValue(operand))
                        return TypeHelpers.NullArgumentType;

                    if (operand is not IConvertible c)
                        return null;

                    var selector = Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture);
                    return selector == 0 ? typeof(int) : typeof(double);
                },
                Calc = (operand, _) =>
                {
                    if (TypeHelpers.IsNullValue(operand))
                        return NullType.Instance;

                    var selector = operand is IConvertible c
                        ? Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture)
                        : 0;
                    return selector == 0 ? 0 : -1.0;
                }
            }
        ]
    };
}
