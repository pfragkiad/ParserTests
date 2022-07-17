namespace ParserLibrary;

using System.Numerics;

public class Vector3Parser : Parser
{
    public Vector3Parser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    { }

    public override object Evaluate(List<Token> postfixTokens, Dictionary<string, object> variables = null)
    {
        if (variables is null) variables = new();

        if (!variables.ContainsKey("pi")) variables.Add("pi", new Complex(Math.PI, 0));
        if (!variables.ContainsKey("e")) variables.Add("e", new Complex(Math.E, 0));

        return base.Evaluate(postfixTokens, variables);
    }

    protected override object EvaluateLiteral(string s) =>
        float.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    public static Vector3 DoubleToVector3(object arg)
        => new Vector3(Convert.ToSingle(arg), Convert.ToSingle(arg), Convert.ToSingle(arg));

    public Vector3 GetVector3UnaryOperand(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var arg = operatorNode.GetUnaryArgument(
             _options.TokenPatterns.UnaryOperatorDictionary[operatorNode.Text].Prefix,
             nodeValueDictionary);

        if (arg is double || arg is int || arg is float) return DoubleToVector3(arg);
        else return (Vector3)arg;
    }

    public (Vector3 Left, Vector3 Right) GetVector3BinaryOperands(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var operands = operatorNode.GetBinaryArguments(nodeValueDictionary);

        return (Left: operands.LeftOperand is double || operands.LeftOperand is int ?
            DoubleToVector3(operands.LeftOperand) : (Vector3)operands.LeftOperand,
                 Right: operands.RightOperand is double || operands.RightOperand is int ?
             DoubleToVector3(operands.RightOperand) : (Vector3)operands.RightOperand);
    }

    public Vector3[] GetVector3FunctionArguments(int count, Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        return functionNode.GetFunctionArguments(count, nodeValueDictionary).Select(arg =>
     {
         if (arg is double || arg is int || arg is float) return DoubleToVector3(arg);
         else return (Vector3)arg;
     }).ToArray();
    }

    #endregion

    protected override object EvaluateUnaryOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        Vector3 operand = GetVector3UnaryOperand(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "-" => -operand,
            "+" => operand,
            "!" => Vector3.Normalize(operand),
            _ => base.EvaluateUnaryOperator(operatorNode, nodeValueDictionary)
        };
    }


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        (Vector3 left, Vector3 right) = GetVector3BinaryOperands(operatorNode, nodeValueDictionary);

        return operatorNode.Text switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "^" => Vector3.Cross(left, right),
            _ => base.EvaluateOperator(operatorNode, nodeValueDictionary)
        };
    }

    protected const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

    HashSet<string> funcsWith3Args = new() { "lerp" };

    HashSet<string> funcsWith2Args = new() { "cross", "dot", "round" };
    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        string functionName = functionNode.Text.ToLower();

        Vector3[] a = GetVector3FunctionArguments(
            count:
            funcsWith2Args.Contains(functionName) ? 2 :
            funcsWith3Args.Contains(functionName) ? 3 :
            1,
            functionNode, nodeValueDictionary);

        switch (functionName)
        {
            case "abs": return Vector3.Abs(a[0]);
            case "cross": return Vector3.Cross(a[0], a[1]);
            case "dot": return Vector3.Dot(a[0], a[1]);
            case "dist": return Vector3.Distance(a[0], a[1]);
            case "dist2": return Vector3.DistanceSquared(a[0], a[1]);
            case "lerp": return Vector3.Lerp(a[0], a[1], a[2].X);
            case "norm": return Vector3.Normalize(a[0]);
            case "sqr":
            case "sqrt": return Vector3.SquareRoot(a[0]);
            case "round":
                return new Vector3(
                (float)Math.Round(a[0].X, (int)a[1].X),
                (float)Math.Round(a[0].Y, (int)a[1].X),
                (float)Math.Round(a[0].Z, (int)a[1].X));
        }

        return base.EvaluateFunction(functionNode, nodeValueDictionary);
    }

}
