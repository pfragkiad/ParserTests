using Microsoft.Extensions.Logging;
using ParserLibrary.Parsers;

namespace ParserTests.Common.Parsers;

/// <summary>
/// Parser supporting mixed arithmetic between int, double and custom Item.
/// Operator semantics (see Item.Operations):
/// +  : int+int -> int
///      numeric (+ promotion) -> double
///      Item ± (int|double) OR (int|double) ± Item -> Item
///      Item + Item -> Item
/// *  : int*int -> int
///      numeric (+ promotion) -> double
///      Item * (int|double) OR (int|double) * Item -> Item
///      (Item * Item) is NOT defined (type inference returns object to signal unsupported)
/// Function:
///   add(Item,int) -> Item
/// </summary>
public class ItemParser(ILogger<ItemParser> logger, ParserServices ps) : ParserBase(logger, ps)
{
    #region Literal parsing / type inference

    protected override object EvaluateLiteral(string s)
    {
        if (int.TryParse(s, out int i))
            return i;
        return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }

    // Faster and exact literal type inference (avoid parsing to value then .GetType()).
    protected override Type EvaluateLiteralType(string s)
        => int.TryParse(s, out _) ? typeof(int) : typeof(double);

    #endregion

    #region Operator evaluation

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

    private static Type? Unwrap(object? o) => o as Type;

    private static bool IsInt(object? o) => Unwrap(o) == typeof(int);
    private static bool IsDouble(object? o) => Unwrap(o) == typeof(double);
    private static bool IsItem(object? o) => Unwrap(o) == typeof(Item);
    private static bool IsNumeric(object? o) => IsInt(o) || IsDouble(o);

    private static Type PromoteNumeric(object? left, object? right)
        => (IsDouble(left) || IsDouble(right)) ? typeof(double) : typeof(int);

    /// <summary>
    /// Precise operator type inference aligned with the actual overloads in Item.Operations.
    /// </summary>
    protected override Type EvaluateOperatorType(string operatorName, object? leftOperand, object? rightOperand)
    {
        // Unknown operand types -> fallback to object (cannot infer)
        if (leftOperand is null || rightOperand is null)
            return typeof(object);

        return operatorName switch
        {
            "+" => InferPlusType(leftOperand, rightOperand),
            "*" => InferMultiplyType(leftOperand, rightOperand),
            _   => base.EvaluateOperatorType(operatorName, leftOperand, rightOperand)
        };
    }

    private static Type InferPlusType(object? left, object? right)
    {
        bool lNum = IsNumeric(left);
        bool rNum = IsNumeric(right);
        bool lItem = IsItem(left);
        bool rItem = IsItem(right);

        if (lNum && rNum) return PromoteNumeric(left, right);        // pure numeric
        if (lItem && rItem) return typeof(Item);                     // Item + Item
        if ((lItem && rNum) || (rItem && lNum)) return typeof(Item); // Item + numeric (int/double) => Item
        // Anything else unsupported/unknown
        return typeof(object);
    }

    private static Type InferMultiplyType(object? left, object? right)
    {
        bool lNum = IsNumeric(left);
        bool rNum = IsNumeric(right);
        bool lItem = IsItem(left);
        bool rItem = IsItem(right);

        if (lNum && rNum) return PromoteNumeric(left, right);               // pure numeric
        if ((lItem && rNum) || (rItem && lNum)) return typeof(Item);        // Item * numeric => Item
        if (lItem && rItem) return typeof(object);                          // No Item * Item overload
        return typeof(object);
    }

    #endregion

    #region Functions

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        if (functionName == "add")
        {
            if (args.Length >= 2 && args[0] is Item && args[1] is int)
                return (Item)args[0]! + (int)args[1]!;
            _logger.LogError("Invalid arguments for function '{FunctionName}': expected (Item,int) got ({A0},{A1})",
                functionName, args.ElementAtOrDefault(0)?.GetType(), args.ElementAtOrDefault(1)?.GetType());
            return null;
        }

        return base.EvaluateFunction(functionName, args);
    }

    protected override Type EvaluateFunctionType(string functionName, object?[] args)
        => functionName switch
        {
            "add" => typeof(Item),
            _     => base.EvaluateFunctionType(functionName, args)
        };

    #endregion
}
