using System.Numerics;

namespace ParserLibrary.Parsers.Common;

public class ComplexParserSession(ILogger<ComplexParser> logger, ParserServices ps) : ParserSessionBase(logger, ps)
{
    public override Dictionary<string, object?> Constants =>
        new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
        {
            { "i", Complex.ImaginaryOne },
            { "j", Complex.ImaginaryOne },
            { "pi", new Complex(Math.PI, 0) },
            { "e", new Complex(Math.E, 0) }
        };

    protected override object EvaluateLiteral(string s, string? group) =>
        double.Parse(s, CultureInfo.InvariantCulture);

    #region Auxiliary functions to get operands

    private static Complex GetComplex(object? value)
    {
        if (value is null) return Complex.Zero;
        if (value is double) return new Complex(Convert.ToDouble(value), 0);
        if (value is not Complex) return Complex.Zero;
        return (Complex)value;
    }

    public static (Complex Left, Complex Right) GetComplexBinaryOperands(object? leftOperand, object? rightOperand) => (
        Left: GetComplex(leftOperand),
        Right: GetComplex(rightOperand)
    );

    public static Complex GetComplexUnaryOperand(object? operand) => GetComplex(operand);

    public static Complex[] GetComplexFunctionArguments(object?[] args) =>
        [.. args.Select(GetComplex)];

    #endregion

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        Complex op = GetComplexUnaryOperand(operand);

        return operatorName switch
        {
            "-" => -op,
            "+" => op,
            _ => base.EvaluateUnaryOperator(operatorName, operand),
        };
    }

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        var (Left, Right) = GetComplexBinaryOperands(leftOperand, rightOperand);
        return operatorName switch
        {
            "+" => Complex.Add(Left, Right),
            "-" => Left - Right,
            "*" => Left * Right,
            "/" => Left / Right,
            "^" => Complex.Pow(Left, Right),
            _ => base.EvaluateOperator(operatorName, leftOperand, rightOperand),
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        Complex[] a = GetComplexFunctionArguments(args);
        const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

        return functionName switch
        {
            "abs" => Complex.Abs(a[0]),
            "acos" => Complex.Acos(a[0]),
            "acosd" => Complex.Acos(a[0]) * TODEG,
            "asin" => Complex.Asin(a[0]),
            "asind" => Complex.Asin(a[0]) * TODEG,
            "atan" => Complex.Atan(a[0]),
            "atand" => Complex.Atan(a[0]) * TODEG,
            "cos" => Complex.Cos(a[0]),
            "cosd" => Complex.Cos(a[0] * TORAD),
            "cosh" => Complex.Cosh(a[0]),
            "exp" => Complex.Exp(a[0]),
            "log" or "ln" => Complex.Log(a[0]),
            "log10" => Complex.Log10(a[0]),
            "log2" => Complex.Log(a[0]) / Complex.Log(2),
            "logn" => Complex.Log(a[0]) / Complex.Log(a[1]),
            "pow" => Complex.Pow(a[0], a[1]),
            "round" => new Complex(Math.Round(a[0].Real, (int)a[1].Real), Math.Round(a[0].Imaginary, (int)a[1].Real)),
            "sin" => Complex.Sin(a[0]),
            "sind" => Complex.Sin(a[0] * TORAD),
            "sinh" => Complex.Sinh(a[0]),
            "sqr" or "sqrt" => Complex.Sqrt(a[0]),
            "tan" => Complex.Tan(a[0]),
            "tand" => Complex.Tan(a[0] * TORAD),
            "tanh" => Complex.Tanh(a[0]),
            _ => base.EvaluateFunction(functionName, args),
        };
    }
}
