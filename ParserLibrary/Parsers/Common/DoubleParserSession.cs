namespace ParserLibrary.Parsers.Common;

public class DoubleParserSession : ParserSessionBase
{
    public DoubleParserSession(ILogger<DoubleParser> logger, ParserServices ps) : base(logger, ps)
    { }

    protected DoubleParserSession(ILogger logger, ParserServices ps) : base(logger, ps) { }


    public override Dictionary<string, object?> Constants =>
        new(_patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
        {
            { "pi", Math.PI },
            { "e", Math.E },
            { "phi", (Math.Sqrt(5.0) + 1.0) / 2.0 }
        };



    protected override object EvaluateLiteral(string s, string? group) =>
        double.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    public static double GetDouble(object? value)
    {
        if (value is null) return 0.0;
        if (value is double d) return d;
        if (value is not IConvertible) return 0.0;
        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    public static (double Left, double Right) GetDoubleBinaryOperands(object? leftOperand, object? rightOperand) => (
        Left: GetDouble(leftOperand),
        Right: GetDouble(rightOperand)
    );


    public static double GetDoubleUnaryOperand(object? operand)
        => GetDouble(operand);



    public static double[] GetDoubleFunctionArguments(object?[] args) =>
        [.. args.Select(GetDouble)];

    #endregion

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        double op = GetDoubleUnaryOperand(operand);
        return operatorName switch
        {
            "-" => -op,
            "+" => op,
            _ => base.EvaluateUnaryOperator(operatorName, operand),
        };
    }


    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        var (left, right) = GetDoubleBinaryOperands(leftOperand, rightOperand);

        return operatorName switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "^" => Math.Pow(left, right),
            _ => base.EvaluateOperator(operatorName, leftOperand, rightOperand),
        };
    }


    //HashSet<string> funcsWith2Args = new() { "atan2","atan2d", "logn","max","min","pow","round"};

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] a = GetDoubleFunctionArguments(args);
        const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

        return functionName switch
        {
            "abs" => Math.Abs(a[0]),
            "acos" => Math.Acos(a[0]),
            "acosd" => Math.Acos(a[0]) * TODEG,
            "acosh" => Math.Acosh(a[0]),
            "asin" => Math.Asin(a[0]),
            "asind" => Math.Asin(a[0]) * TODEG,
            "asinh" => Math.Asinh(a[0]),
            "atan" => Math.Atan(a[0]),
            "atand" => Math.Atan(a[0]) * TODEG,
            "atan2" => Math.Atan2(a[0], a[1]),// y/x
            "atan2d" => Math.Atan2(a[0], a[1]) * TODEG,// y/x
            "atanh" => Math.Atanh(a[0]),
            "cbrt" => Math.Cbrt(a[0]),
            "cos" => Math.Cos(a[0]),
            "cosd" => Math.Cos(a[0] * TORAD),
            "cosh" => Math.Cosh(a[0]),
            "exp" => Math.Exp(a[0]),
            "log" or "ln" => Math.Log(a[0]),
            "log10" => Math.Log10(a[0]),
            "log2" => Math.Log2(a[0]),
            "logn" => Math.Log(a[0]) / Math.Log(a[1]),
            "max" => Math.Max(a[0], a[1]),
            "min" => Math.Min(a[0], a[1]),
            "pow" => Math.Pow(a[0], a[1]),
            "round" => Math.Round(a[0], (int)a[1]),
            "sin" => Math.Sin(a[0]),
            "sind" => Math.Sin(a[0] * TORAD),
            "sinh" => Math.Sinh(a[0]),
            "sqr" or "sqrt" => Math.Sqrt(a[0]),
            "tan" => Math.Tan(a[0]),
            "tand" => Math.Tan(a[0] * TORAD),
            "tanh" => Math.Tanh(a[0]),
            _ => base.EvaluateFunction(functionName, args),
        };
    }

}
