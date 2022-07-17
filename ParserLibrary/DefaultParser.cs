namespace ParserLibrary;

public class DefaultParser : Parser
{
    public DefaultParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    { }

    /// <summary>
    /// Overriding the Evaluate function is great for adding custom "constant" literals.
    /// </summary>
    /// <param name="postfixTokens"></param>
    /// <param name="variables"></param>
    /// <returns></returns>
    public override object Evaluate(List<Token> postfixTokens, Dictionary<string, object> variables = null)
    {
        if (variables is null)
            variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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
        var operands = operatorNode.GetBinaryArguments(nodeValueDictionary);
        return (Left: Convert.ToDouble(operands.LeftOperand),
                 Right: Convert.ToDouble(operands.RightOperand));
    }

    public double[] GetDoubleFunctionArguments(int count, Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        return functionNode.GetFunctionArguments(count ,nodeValueDictionary).Select(a => Convert.ToDouble(a)).ToArray();
    }

    #endregion

    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //double operand = Convert.ToDouble(nodeValueDictionary[
        //    (_options.TokenPatterns.UnaryOperatorDictionary[operatorNode.Text].Prefix ?
        //    operatorNode.Right : operatorNode.Left) as Node<Token>]);

        double operand = GetDoubleUnaryOperand(operatorNode, nodeValueDictionary);

        switch (operatorNode.Text)
        {
            case "-": return -operand;
            case "+": return operand;
            default: return base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary);


        }
    }


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        //double left = Convert.ToDouble(nodeValueDictionary[operatorNode.Left as Node<Token>]);
        //double right = Convert.ToDouble(nodeValueDictionary[operatorNode.Right as Node<Token>]);
        (double left, double right) = GetDoubleBinaryOperands(operatorNode, nodeValueDictionary);

        switch (operatorNode.Text)
        {
            case "+": return left + right;
            case "-": return left - right;
            case "*": return left * right;
            case "/": return left / right;
            case "^": return Math.Pow(left, right);
            default: return base.EvaluateOperator(operatorNode, nodeValueDictionary);

        }
    }

    protected const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

    HashSet<string> funcsWith2Args = new() { "atan2","atan2d", "logn","max","min","pow","round"};

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text.ToLower();
       
        double[] a = GetDoubleFunctionArguments(
            count:funcsWith2Args.Contains(functionName) ? 2 : 1,
            functionNode, nodeValueDictionary);

        switch (functionName)
        {
            case "abs": return Math.Abs(a[0]);
            case "acos": return Math.Acos(a[0]);
            case "acosd": return Math.Acos(a[0]) * TODEG;
            case "acosh": return Math.Acosh(a[0]);
            case "asin": return Math.Asin(a[0]);
            case "asind": return Math.Asin(a[0]) * TODEG;
            case "asinh": return Math.Asinh(a[0]);
            case "atan": return Math.Atan(a[0]);
            case "atand": return Math.Atan(a[0]) * TODEG;
            case "atan2": return Math.Atan2(a[0], a[1]); // y/x
            case "atan2d": return Math.Atan2(a[0], a[1]) * TODEG; // y/x
            case "atanh": return Math.Atanh(a[0]);
            case "cbrt": return Math.Cbrt(a[0]);
            case "cos": return Math.Cos(a[0]);
            case "cosd": return Math.Cos(a[0] * TORAD);
            case "cosh": return Math.Cosh(a[0]);
            case "exp": return Math.Exp(a[0]);
            case "log":
            case "ln": return Math.Log(a[0]);
            case "log10": return Math.Log10(a[0]);
            case "log2": return Math.Log2(a[0]);
            case "logn": return Math.Log(a[0]) / Math.Log(a[1]);
            case "max": return Math.Max(a[0], a[1]);
            case "min": return Math.Min(a[0], a[1]);
            case "pow": return Math.Pow(a[0], a[1]);
            case "round": return Math.Round(a[0], (int)a[1]);
            case "sin": return Math.Sin(a[0]);
            case "sind": return Math.Sin(a[0] * TORAD);
            case "sinh": return Math.Sinh(a[0]);
            case "sqr":
            case "sqrt": return Math.Sqrt(a[0]);
            case "tan": return Math.Tan(a[0]);
            case "tand": return Math.Tan(a[0] * TORAD);
            case "tanh": return Math.Tanh(a[0]);
        }

        return base.EvaluateFunction(functionNode, nodeValueDictionary);
    }

}
