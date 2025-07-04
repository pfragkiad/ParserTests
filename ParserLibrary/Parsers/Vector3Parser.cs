﻿namespace ParserLibrary.Parsers;

using ParserLibrary.Tokenizers;
using System.Numerics;

public class Vector3Parser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : Parser(logger, options)
{
    protected override object? Evaluate(List<Token> postfixTokens, Dictionary<string, object?>? variables = null)
    {
        variables ??= new Dictionary<string, object?>(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        if (!variables.ContainsKey("pi")) variables.Add("pi", DoubleToVector3((float)Math.PI));
        if (!variables.ContainsKey("e")) variables.Add("e", DoubleToVector3((float)Math.E));

        if (!variables.ContainsKey("ux")) variables.Add("ux", Vector3.UnitX);
        if (!variables.ContainsKey("uy")) variables.Add("uy", Vector3.UnitY);
        if (!variables.ContainsKey("uz")) variables.Add("uz", Vector3.UnitZ);

        return base.Evaluate(postfixTokens, variables);
    }

    protected override object EvaluateLiteral(string s) =>
        float.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    public static Vector3 DoubleToVector3(object arg)
        => new(Convert.ToSingle(arg), Convert.ToSingle(arg), Convert.ToSingle(arg));


    public static bool IsNumeric(object arg) =>
           arg is double || arg is int || arg is float;

    public static Vector3 GetVector3(object? arg)
    {
        if (arg is null) return Vector3.Zero;
        if (IsNumeric(arg)) return DoubleToVector3(arg);
        if (arg is Vector3 v) return v;
        return Vector3.Zero;
    }

    public static Vector3 GetVector3UnaryOperand(object? operand) =>
        GetVector3(operand);

    public static (Vector3 Left, Vector3 Right) GetVector3BinaryOperands(object? leftOperand, object? rightOperand) => (
        Left: GetVector3(leftOperand),
        Right: GetVector3(rightOperand)
    );

    public static Vector3[] GetVector3FunctionArguments(object?[] args) =>
        [.. args.Select(GetVector3)];

    #endregion

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        Vector3 op = GetVector3UnaryOperand(operand);

        return operatorName switch
        {
            "-" => -op,
            "+" => op,
            "!" => Vector3.Normalize(op),
            _ => base.EvaluateUnaryOperator(operatorName, operand)
        };
    }


    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        (Vector3 left, Vector3 right) = GetVector3BinaryOperands(leftOperand,rightOperand);

        return operatorName switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "^" => Vector3.Cross(left, right),
            "@" => Vector3.Dot(left, right),
            _ => base.EvaluateOperator(operatorName, leftOperand,rightOperand)
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        Vector3[] a = GetVector3FunctionArguments(args);

        return functionName switch
        {
            "abs" => Vector3.Abs(a[0]),
            "cross" => Vector3.Cross(a[0], a[1]),
            "dot" => Vector3.Dot(a[0], a[1]),
            "dist" => Vector3.Distance(a[0], a[1]),
            "dist2" => Vector3.DistanceSquared(a[0], a[1]),
            "lerp" => Vector3.Lerp(a[0], a[1], a[2].X),
            "length" => a[0].Length(),
            "length2" => a[0].LengthSquared(),
            "norm" => Vector3.Normalize(a[0]),
            "sqr" or "sqrt" => Vector3.SquareRoot(a[0]),
            "round" => new Vector3(
                            (float)Math.Round(a[0].X, (int)a[1].X),
                            (float)Math.Round(a[0].Y, (int)a[1].X),
                            (float)Math.Round(a[0].Z, (int)a[1].X)),
            _ => base.EvaluateFunction(functionName, args),
        };
    }

}
