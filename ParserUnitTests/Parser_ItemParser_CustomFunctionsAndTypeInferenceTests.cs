using ParserLibrary;
using ParserLibrary.Parsers.Common;
using ParserLibrary.Parsers.Interfaces;
using ParserTests.Common;
using ParserTests.Common.Parsers;
using Xunit;

namespace ParserUnitTests;

public class Parser_ItemParser_CustomFunctionsAndTypeInferenceTests
{
    private static IParser GetItemParser() => ParserApp.GetParser<ItemParser>();

    private static IParser GetDoubleParser() => ParserApp.GetParser<DoubleParser>();

    [Fact]
    public void Evaluate_Item_Add_Function_And_Int_Produces_Item_Type()
    {
        var parser = GetItemParser();
        var a = new Item { Name = "foo", Value = 3 };
        var b = new Item { Name = "bar", Value = 5 };

        var expr = "a + add(b,4) + 5";
        var result = parser.Evaluate(expr, new()
        {
            { "a", a },
            { "b", b }
        });

        Assert.IsType<Item>(result);
        var t = parser.EvaluateType(expr, new()
        {
            { "a", a },
            { "b", b }
        });
        Assert.Equal(typeof(Item), t);
    }

    [Fact]
    public void Evaluate_ItemPlusInt_TypeInference_Item()
    {
        var parser = GetItemParser();
        var a = new Item { Name = "A", Value = 10 };
        var expr = "a+10";

        var v = parser.Evaluate(expr, new() { { "a", a } });
        Assert.IsType<Item>(v);

        var t = parser.EvaluateType(expr, new() { { "a", a } });
        Assert.Equal(typeof(Item), t);
    }

    [Fact]
    public void Evaluate_ItemPlusTwoDoubles_TypeInference_Double()
    {
        var parser = GetItemParser();
        var a = new Item { Name = "A", Value = 10 };
        var expr = "a+10.7+90.8";

        Dictionary<string, object?> variables = new(StringComparer.OrdinalIgnoreCase)
            { ["a"]= a  };

        var v = parser.Evaluate(expr, variables);
        Assert.IsType<Item>(v);

        var t = parser.EvaluateType(expr, variables);
        Assert.Equal(typeof(Item), t);
    }

    [Fact]
    public void CustomRegisteredFunction_Evaluates_And_TypeInference_Int()
    {
        var parser = GetItemParser();
        parser.RegisterFunction("myfunc(a,b) = a + b + 10");

        var expr = "myfunc(a,10)";
        var v = parser.Evaluate(expr, new() { { "a", 500 } });
        Assert.IsType<int>(v);
        Assert.Equal(520, (int)v!);

        var t = parser.EvaluateType(expr, new() { { "a", 500 } });
        Assert.Equal(typeof(int), t);
    }
}