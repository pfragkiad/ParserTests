using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserUnitTests.Parsers;


public class Item
{
    public required string Name { get; set; }

    public int Value { get; set; } = 0;

    //we define a custom operator for the type to simplify the evaluateoperator example later
    //this is not 100% needed, but it keeps the code in the CustomTypeParser simpler
    public static Item operator +(int v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };
    public static Item operator +(Item v2, int v1) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v1, Item v2) =>
        new() { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    public override string ToString() => $"{Name} {Value}";

}

public class CustomTypeParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : Parser(logger, options)
{

    //we assume that literals are integer numbers only
    protected override object EvaluateLiteral(string s) => int.Parse(s);

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {

        if (operatorName == "+")
        {
            //ADDED:
            _logger.LogDebug("Adding with + operator ${left} and ${right}", leftOperand, rightOperand);


            //we manage all combinations of Item/Item, Item/int, int/Item combinations here
            if (leftOperand is Item left && rightOperand is Item right)
                return left + right;

            return leftOperand is Item ?
                (Item)leftOperand + (int)rightOperand! : (int)leftOperand! + (Item)rightOperand!;
        }


        return base.EvaluateOperator(operatorName, leftOperand, rightOperand);
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        if (args[0] is not Item || args[1] is not int)
        {
            _logger.LogError("Invalid arguments for function {FunctionName}: {Args}", functionName, args);
            throw new ArgumentException($"Invalid arguments for function {functionName}");
        }

        return functionName switch
        {
            "add" => (Item)args[0]! + (int)args[1]!,
            _ => base.EvaluateFunction(functionName, args)
        };
    }

}

public class CustomTypeTransientParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : TransientParser(logger, options)
{

    //we assume that literals are integer numbers only
    protected override object EvaluateLiteral(string s) => int.Parse(s);

    protected override object EvaluateOperator(Node<Token> operatorNode)
    {
        (object LeftOperand, object RightOperand) = GetBinaryArguments(operatorNode);

        if (operatorNode.Text == "+")
        {
            _logger.LogDebug("Adding with + operator ${left} and ${right}", LeftOperand, RightOperand);


            //we manage all combinations of Item/Item, Item/int, int/Item combinations here
            if (LeftOperand is Item && RightOperand is Item)
                return (Item)LeftOperand + (Item)RightOperand;

            return LeftOperand is Item ? (Item)LeftOperand + (int)RightOperand : (int)LeftOperand + (Item)RightOperand;
        }

        return base.EvaluateOperator(operatorNode);
    }

    protected override object EvaluateFunction(Node<Token> functionNode)
    {
        var a = GetFunctionArguments(functionNode);

        //MODIFIED: used the CaseSensitive from the options in the configuration file. The options are retrieved via dependency injection.
        //return functionNode.Text switch
        return _options.CaseSensitive ? functionNode.Text.ToLower() : functionNode.Text switch
        {
            "add" => (Item)a[0] + (int)a[1],
            _ => base.EvaluateFunction(functionNode)
        };
    }

}
