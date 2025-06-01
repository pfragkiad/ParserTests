using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers;
using ParserLibrary.Tokenizers;

namespace ParserTests.Item;

public class ItemParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : Parser(logger, tokenizer, options)
{

    //we assume that LITERALS are integer numbers only
    protected override object EvaluateLiteral(string s)
    {
        //return int if parsed else double
        if (int.TryParse(s, out int i))
            return i;

        return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }


    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        (object LeftOperand, object RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);

        if (operatorNode.Text == "+")
        {
            //ADDED:
            _logger.LogDebug("Adding with + operator ${left} and ${right}", LeftOperand, RightOperand);


            ////we manage all combinations of Item/Item, Item/int, int/Item combinations here
            ////use scopes to allow left/right names with different types
            //{
            //    if (LeftOperand is Item left && RightOperand is Item right)
            //        return left + right;
            //}

            //{
            //    if (LeftOperand is Item left && RightOperand is double right)
            //        return left + right;
            //}

            //{
            //    if (LeftOperand is double left && RightOperand is Item right)
            //        return left + right;
            //}

            //{
            //    if (LeftOperand is int left && RightOperand is Item right)
            //        return left + right;
            //}

            //{
            //    if (LeftOperand is Item left && RightOperand is int right)
            //        return left + right;
            //}

            dynamic left = LeftOperand, right = RightOperand;
            return left + right;


#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
                              //return LeftOperand is Item ? (Item)LeftOperand + (int)RightOperand : (int)LeftOperand + (Item)RightOperand;
        }

        return base.EvaluateOperator(operatorNode, nodeValueDictionary);
    }

    //needed when evaluating type ONLY
    protected override Type EvaluateOperatorType(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        (object LeftOperand, object RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);


        /*
    public static Item operator +(int v1, Item v2) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v2, int v1) =>
        new() { Name = v2.Name, Value = v2.Value + v1 };


    //check return as double
    public static double operator +(Item v2, double v1) =>
        v2.Value + v1;

    public static double operator +(double v1, Item v2) =>
        v2.Value + v1;

    public static Item operator +(Item v1, Item v2) =>
        new() { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };
         */
        bool isLeftInt = LeftOperand as Type == typeof(int);
        bool isRightInt = RightOperand as Type == typeof(int);
        bool isLeftNumeric = LeftOperand as Type == typeof(int) || LeftOperand as Type == typeof(double);
        bool isRightNumeric = RightOperand as Type == typeof(int) || RightOperand as Type == typeof(double);
        bool isLeftItem = LeftOperand as Type == typeof(Item);
        bool isRightItem = RightOperand as Type == typeof(Item);

        if (operatorNode.Text == "+")
        {
            if (isLeftInt && isRightInt) return typeof(int);
            if(isLeftNumeric && isRightNumeric) return typeof(double); //all other numeric combinations return double
            if (isLeftItem && isRightItem) return typeof(Item);
            if(isLeftInt || isRightInt) return typeof(Item); //int + Item or Item + int returns Item
            if (isLeftItem || isRightItem) return typeof(Item); //Item + double or double + Item returns Item
        }
        return null;
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var a = functionNode.GetFunctionArguments(2, nodeValueDictionary);

        //MODIFIED: used the CaseSensitive from the options in the configuration file. The options are retrieved via dependency injection.
        //return functionNode.Text switch   
        return _options.CaseSensitive ? functionNode.Text.ToLower() : functionNode.Text switch
        {
            "add" => (Item)a[0] + (int)a[1],

            //at the end check for custom functions
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
        };
    }


    //needed when evaluating type ONLY

    protected override Type EvaluateFunctionType(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var a = functionNode.GetFunctionArguments(2, nodeValueDictionary);

        //MODIFIED: used the CaseSensitive from the options in the configuration file. The options are retrieved via dependency injection.
        //return functionNode.Text switch   
        return (_options.CaseSensitive ? functionNode.Text.ToLower() : functionNode.Text) switch
        {
            "add" => typeof(Item),

            //at the end check for custom functions
            _ => base.EvaluateFunctionType(functionNode, nodeValueDictionary)
        };
    }

}


public class CustomTypeTransientParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : TransientParser(logger, tokenizer, options)
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
