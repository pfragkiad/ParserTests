namespace ParserLibrary;

using System.Numerics;

public class ComplexParser : Parser
{
    public ComplexParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    { }

    public override object Evaluate(List<Token> postfixTokens, Dictionary<string, object> variables = null)
    {
        if (variables is null) variables = new();

        if (!variables.ContainsKey("i")) variables.Add("i", Complex.ImaginaryOne);
        if (!variables.ContainsKey("j")) variables.Add("j", Complex.ImaginaryOne);
        if (!variables.ContainsKey("pi")) variables.Add("pi", new Complex(Math.PI,0));
        if (!variables.ContainsKey("e")) variables.Add("e", new Complex(Math.E,0));

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
        var operands = operatorNode.GetBinaryArguments(nodeValueDictionary);

        return (Left: operands.LeftOperand is double || operands.LeftOperand is int ?
            new Complex(Convert.ToDouble(operands.LeftOperand), 0) : (Complex)operands.LeftOperand,
                 Right: operands.RightOperand is double || operands.RightOperand is int ?
            new Complex(Convert.ToDouble(operands.RightOperand), 0) : (Complex)operands.RightOperand);
    }

    public Complex[] GetComplexFunctionArguments(int count, Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        return functionNode.GetFunctionArguments(count,nodeValueDictionary).Select(arg =>
     {
         if (arg is double || arg is int) return new Complex(Convert.ToDouble(arg), 0);
         else return (Complex)arg;
     }).ToArray();
    }

    #endregion

    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        Complex operand = GetComplexUnaryOperand(operatorNode, nodeValueDictionary);

        switch (operatorNode.Text)
        {
            case "-": return -operand;
            case "+": return operand;
            default: return base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary);
        }
    }


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        (Complex left, Complex right) = GetComplexBinaryOperands(operatorNode, nodeValueDictionary);

        switch (operatorNode.Text)
        {
            case "+": return Complex.Add(left, right);
            case "-": return left - right;
            case "*": return left * right;
            case "/": return left / right;
            case "^": return Complex.Pow(left, right);
            default: return base.EvaluateOperator(operatorNode, nodeValueDictionary);

        }
    }

    protected const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

    HashSet<string> funcsWith2Args = new() { "logn", "pow", "round" };
    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text.ToLower();
       
        Complex[] a =  GetComplexFunctionArguments(
            count:  funcsWith2Args.Contains(functionName) ? 2 : 1,
            functionNode, nodeValueDictionary);

        switch (functionName)
        {
            case "abs": return Complex.Abs(a[0]);
            case "acos": return Complex.Acos(a[0]);
            case "acosd": return Complex.Acos(a[0]) * TODEG;
            case "asin": return Complex.Asin(a[0]);
            case "asind": return Complex.Asin(a[0]) * TODEG;
            case "atan": return Complex.Atan(a[0]);
            case "atand": return Complex.Atan(a[0]) * TODEG;
            case "cos": return Complex.Cos(a[0]);
            case "cosd": return Complex.Cos(a[0] * TORAD);
            case "cosh": return Complex.Cosh(a[0]);
            case "exp": return Complex.Exp(a[0]);
            case "log":
            case "ln": return Complex.Log(a[0]);
            case "log10": return Complex.Log10(a[0]);
            case "log2": return Complex.Log(a[0]) / Complex.Log(2);
            case "logn": return Complex.Log(a[0]) / Complex.Log(a[1]);
            case "pow": return Complex.Pow(a[0], a[1]);
            case "round": return new Complex(Math.Round(a[0].Real, (int)a[1].Real), Math.Round(a[0].Imaginary, (int)a[1].Real));
            case "sin": return Complex.Sin(a[0]);
            case "sind": return Complex.Sin(a[0] * TORAD);
            case "sinh": return Complex.Sinh(a[0]);
            case "sqr":
            case "sqrt": return Complex.Sqrt(a[0]);
            case "tan": return Complex.Tan(a[0]);
            case "tand": return Complex.Tan(a[0] * TORAD);
            case "tanh": return Complex.Tanh(a[0]);
        }

        return base.EvaluateFunction(functionNode, nodeValueDictionary);
    }

}
