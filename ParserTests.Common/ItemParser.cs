using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserTests.Common;

public class ItemParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : Parser(logger, options)
{
    protected override object EvaluateLiteral(string s)
    {
        //return int if parsed else double
        if (int.TryParse(s, out int i))
            return i;

        return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        if (leftOperand is null || rightOperand is null)
        {
            _logger.LogError("Invalid operands for operator {OperatorName}: {LeftOperand}, {RightOperand}", operatorName, leftOperand, rightOperand);
            throw new ArgumentException($"Invalid operands for operator {operatorName}");
        }

        return operatorName switch
        {
            "+" => (dynamic)leftOperand! + (dynamic)rightOperand!,
            "*" => (dynamic)leftOperand! * (dynamic)rightOperand!,
            _ => base.EvaluateOperator(operatorName, leftOperand, rightOperand)
        };
    }

    protected override Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand)
    {

        bool isLeftInt = leftOperand as Type == typeof(int);
        bool isRightInt = rightOperand as Type == typeof(int);
        bool isLeftNumeric = leftOperand as Type == typeof(int) || leftOperand as Type == typeof(double);
        bool isRightNumeric = rightOperand as Type == typeof(int) || rightOperand as Type == typeof(double);
        bool isLeftItem = leftOperand is Type t && t == typeof(Item);
        bool isRightItem = rightOperand is Type t2 && t2 == typeof(Item);

        return operatorName switch
        {
            "+" => (isLeftInt && isRightInt) ? typeof(int) :
                   (isLeftNumeric && isRightNumeric) ? typeof(double) :
                   (isLeftItem && isRightItem) ? typeof(Item) :
                   (isLeftInt || isRightInt) ? typeof(Item) :
                   (isLeftItem || isRightItem) ? typeof(double) :
                   typeof(object),
            "*" => (isLeftInt && isRightInt) ? typeof(int) :
                   (isLeftNumeric && isRightNumeric) ? typeof(double) :
                   (isLeftItem && (isRightInt || isRightNumeric)) ? typeof(Item) :
                   ((isLeftInt || isLeftNumeric) && isRightItem) ? typeof(Item) :
                   typeof(object),
            _ => base.EvaluateOperatorType(operatorName, leftOperand, rightOperand)
        };
    }


    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        if (args[0] is not Item || args[1] is not int)
        {
            _logger.LogError("Invalid arguments for function '{functionName}': expected Item and int, got {arg0} and {arg1}", functionName, args[0]?.GetType(), args[1]?.GetType());
            return null;
        }

        return functionName switch
        {
            "add" => (Item)args[0]! + (int)args[1]!,
            _ => base.EvaluateFunction(functionName, args)
        };
    }

    protected override Type EvaluateFunctionType(string functionName, object?[] args)
    {
        return functionName switch
        {
            "add" => typeof(Item),
            _ => base.EvaluateFunctionType(functionName, args)
        };

    }
}
