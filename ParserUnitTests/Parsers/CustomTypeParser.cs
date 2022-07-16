using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserUnitTests.Parsers;


public class Item
{
    public string Name { get; set; }

    public int Value { get; set; } = 0;

    //we define a custom operator for the type to simplify the evaluateoperator example later
    public static Item operator +(int v1, Item v2) =>
        new Item { Name = v2.Name , Value = v2.Value + v1 };
    public static Item operator +(Item v2, int v1) =>
        new Item { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v1, Item v2) =>
        new Item { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    public override string ToString() => $"{Name} {Value}";

}

public class CustomTypeParser : Parser
{
    public CustomTypeParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    { }


    //we assume that literals are integer numbers only
    protected override object EvaluateLiteral(string s) => int.Parse(s);

    protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        (object LeftOperand, object RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);

        //we assume the + operator
        if (operatorNode.Text == "+")
        {
            //we manage all combinations of Item/Item, Item/int, int/Item combinations here
            if (LeftOperand is Item && RightOperand is Item)
                return (Item)LeftOperand + (Item)RightOperand;

            return LeftOperand is Item ?  (Item)LeftOperand + (int)RightOperand : (int)LeftOperand + (Item)RightOperand;
        }

        return base.EvaluateOperator(operatorNode, nodeValueDictionary);
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        var a = functionNode.GetFunctionArguments(nodeValueDictionary);

        return functionNode.Text switch
        {
            "add" => (Item)a[0] + (int)a[1],
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
        };
    }

}
