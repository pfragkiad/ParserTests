using Microsoft.Extensions.Logging;
using ParserLibrary.Definitions;
using ParserLibrary.Parsers;

namespace ParserTests.Common.Parsers;

public class ItemParserSession(ILogger<ItemParserSession> logger, ParserServices ps) : ParserSessionBase(logger, ps)
{
    //we assume that LITERALS are integer numbers only
    protected override object EvaluateLiteral(string s, string? group)
    {
        //return int if parsed else double
        if (int.TryParse(s, out int i))
            return i;

        return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        return operatorName switch
        {
            "+" => operand,//unary plus does nothing
            "-" => -(operand as dynamic),
            "%" => (operand as dynamic) * 10,
            _ => base.EvaluateUnaryOperator(operatorName, operand),
        };
    }

    protected override Type EvaluateUnaryOperatorType(string operatorName, object? operand)
    {
        return typeof(int);
    }

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        if (leftOperand is null || rightOperand is null)
        {
            _logger.LogError("Invalid operands for operator {OperatorName}: {LeftOperand}, {RightOperand}", operatorName, leftOperand, rightOperand);
            throw new ArgumentException($"Invalid operands for operator {operatorName}");
        }

        if (operatorName == "+")
        {
            _logger.LogDebug("Adding with + operator {Left} and {Right}", leftOperand, rightOperand);
            dynamic left = leftOperand!, right = rightOperand!;
            return left + right;
        }
        else if (operatorName == "*")
        {
            _logger.LogDebug("Multiplying with * operator {Left} and {Right}", leftOperand, rightOperand);
            dynamic left = leftOperand!, right = rightOperand!;
            return left * right;
        }

        return base.EvaluateOperator(operatorName, leftOperand, rightOperand);
    }

    protected override Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand)
    {
        var leftType = OperatorDefinition.GetArgumentType(leftOperand);
        var rightType = OperatorDefinition.GetArgumentType(rightOperand);

        bool isLeftInt = leftType == typeof(int);
        bool isRightInt = rightType == typeof(int);
        bool isLeftNumeric = leftType == typeof(int) || leftType == typeof(double);
        bool isRightNumeric = rightType == typeof(int) || rightType == typeof(double);
        bool isLeftItem = leftType == typeof(Item);
        bool isRightItem = rightType == typeof(Item);

        if (operatorName == "+")
        {
            if (isLeftInt && isRightInt) return typeof(int);
            if (isLeftNumeric && isRightNumeric) return typeof(double); //all other numeric combinations return double
            if (isLeftItem && isRightItem) return typeof(Item);
            if (isLeftInt || isRightInt) return typeof(Item); //int + Item or Item + int returns Item
            if (isLeftItem || isRightItem) return typeof(double); //Item + double or double + Item returns double
        }
        else if(operatorName == "*")
        {
            if (isLeftInt && isRightInt) return typeof(int);
            if (isLeftNumeric && isRightNumeric) return typeof(double); //all other numeric combinations return double
            if ((isLeftItem && isRightInt) || (isLeftInt && isRightItem)) return typeof(Item); //Item * int or int * Item returns Item
            if ((isLeftItem && isRightNumeric) || (isLeftNumeric && isRightItem)) return typeof(double); //Item * double or double * Item returns double
        }

        return base.EvaluateOperatorType(operatorName, leftOperand, rightOperand);
    }

    // Respect case sensitivity for built-in function metadata lookups
    protected override Dictionary<string, byte> MainFunctionsWithFixedArgumentsCount =>
        new(_patterns.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
        {
            ["add"] = 2,
            ["tre"] = 0
        };

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        // Normalize function name according to current case-sensitivity rules
        var name = _patterns.CaseSensitive ? functionName : functionName.ToLowerInvariant();

        //if (args.Length < 2 || args[0] is not Item || args[1] is not int)
        //{
        //    _logger.LogError("Invalid arguments for function '{FunctionName}': expected Item and int, got {Arg0} and {Arg1}",
        //        name, args.ElementAtOrDefault(0)?.GetType(), args.ElementAtOrDefault(1)?.GetType());
        //    return null;
        //}

        return name switch
        {
            "add" => (Item)args[0]! + (int)args[1]!,
            "tre" => 100,
            _ => base.EvaluateFunction(name, args)
        };
    }

    protected override Type EvaluateFunctionType(string functionName, object?[] args)
    {
        // Normalize function name according to current case-sensitivity rules
        var name = _patterns.CaseSensitive ? functionName : functionName.ToLowerInvariant();

        return name switch
        {
            "add" => typeof(Item),
            "tre" => typeof(int),
            _ => base.EvaluateFunctionType(name, args)
        };
    }
}
