using System.Numerics;

namespace ParserLibrary.Parsers;

public class ComplexParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : Parser(logger, tokenizer, options)
{
    protected override object Evaluate(List<Token> postfixTokens, Dictionary<string, object>? variables = null)
    {
        variables ??= [];

        //we define "constants" if they are not already defined
        if (!variables.ContainsKey("i")) variables.Add("i", Complex.ImaginaryOne);
        if (!variables.ContainsKey("j")) variables.Add("j", Complex.ImaginaryOne);
        if (!variables.ContainsKey("pi")) variables.Add("pi", new Complex(Math.PI, 0));
        if (!variables.ContainsKey("e")) variables.Add("e", new Complex(Math.E, 0));

        return base.Evaluate(postfixTokens, variables);
    }

    protected override object EvaluateLiteral(string s) =>
        double.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    public Complex GetComplexUnaryOperand(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var arg = operatorNode.GetUnaryArgument(
             _options.TokenPatterns.UnaryOperatorDictionary[operatorNode.Text].Prefix,
             nodeValueDictionary);

        if (arg is double || arg is int) return new Complex(Convert.ToDouble(arg), 0);
        else return (Complex)arg;
    }

    public (Complex Left, Complex Right) GetComplexBinaryOperands(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);

        return (Left: LeftOperand is double || LeftOperand is int ?
            new Complex(Convert.ToDouble(LeftOperand), 0) : (Complex)LeftOperand,
                 Right: RightOperand is double || RightOperand is int ?
            new Complex(Convert.ToDouble(RightOperand), 0) : (Complex)RightOperand);
    }

    public Complex[] GetComplexFunctionArguments(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        return [.. functionNode.GetFunctionArguments(
            _options.TokenPatterns.ArgumentSeparator,
            nodeValueDictionary).Select(arg =>
     {
         if (arg is double || arg is int) return new Complex(Convert.ToDouble(arg), 0);
         else return (Complex)arg;
     })];
    }

    #endregion

    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        Complex operand = GetComplexUnaryOperand(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "-" => -operand,
            "+" => operand,
            _ => base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary),
        };
    }


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        (Complex left, Complex right) = GetComplexBinaryOperands(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "+" => Complex.Add(left, right),
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "^" => Complex.Pow(left, right),
            _ => base.EvaluateOperator(operatorNode, nodeValueDictionary),
        };
    }

    protected const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

    //HashSet<string> funcsWith2Args = new() { "logn", "pow", "round" };

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text.ToLower();

        Complex[] a = GetComplexFunctionArguments(functionNode, nodeValueDictionary);

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
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary),
        };
    }

}
