using ParserLibrary.Tokenizers;

namespace ParserLibrary.Parsers;

public class DefaultParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : Parser(logger, tokenizer, options)
{

    /// <summary>
    /// Overriding the Evaluate function is great for adding custom "constant" literals.
    /// </summary>
    /// <param name="postfixTokens"></param>
    /// <param name="variables"></param>
    /// <returns></returns>
    protected override object Evaluate(List<Token> postfixTokens, Dictionary<string, object>? variables = null)
    {
        variables ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!variables.ContainsKey("pi")) variables.Add("pi", Math.PI);
        if (!variables.ContainsKey("e")) variables.Add("e", Math.E);
        if (!variables.ContainsKey("phi")) variables.Add("phi", (Math.Sqrt(5.0) + 1.0) / 2.0);

        return base.Evaluate(postfixTokens, variables);
    }

    protected override object EvaluateLiteral(string s) =>
        double.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    public double GetDoubleUnaryOperand(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
        => Convert.ToDouble(
            operatorNode.GetUnaryArgument(
            _options.TokenPatterns.UnaryOperatorDictionary[operatorNode.Text].Prefix,
            nodeValueDictionary));
    public (double Left, double Right) GetDoubleBinaryOperands(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var (LeftOperand, RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);
        return (Left: Convert.ToDouble(LeftOperand),
                 Right: Convert.ToDouble(RightOperand));
    }

    public double[] GetDoubleFunctionArguments(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        return [.. functionNode.GetFunctionArguments(_options.TokenPatterns.ArgumentSeparator, nodeValueDictionary).Select(a => Convert.ToDouble(a))];
    }

    #endregion

    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //double operand = Convert.ToDouble(nodeValueDictionary[
        //    (_options.TokenPatterns.UnaryOperatorDictionary[operatorNode.Text].Prefix ?
        //    operatorNode.Right : operatorNode.Left) as Node<Token>]);

        double operand = GetDoubleUnaryOperand(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "-" => -operand,
            "+" => operand,
            _ => base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary),
        };
    }


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //double left = Convert.ToDouble(nodeValueDictionary[operatorNode.Left as Node<Token>]);
        //double right = Convert.ToDouble(nodeValueDictionary[operatorNode.Right as Node<Token>]);
        (double left, double right) = GetDoubleBinaryOperands(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "^" => Math.Pow(left, right),
            _ => base.EvaluateOperator(operatorNode, nodeValueDictionary),
        };
    }

    protected const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

    //HashSet<string> funcsWith2Args = new() { "atan2","atan2d", "logn","max","min","pow","round"};

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text.ToLower();

        double[] a = GetDoubleFunctionArguments(
            //count:funcsWith2Args.Contains(functionName) ? 2 : 1,
            functionNode, nodeValueDictionary);

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
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary),
        };
    }

}
