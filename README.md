# ParserLibrary
_No Other Expression Parser, Ever_

### How it began
I wanted to write my "custom terminal" that used interactive commands with expressions. Other expression builders used only "numbers" as basic entities which I did not want; this is something too common. I wanted some variables to represent "musical notes" or "musical chords", or even "vectors" and "matrices" and some other to represent numbers.
The only way, was to build an Expression builder that could allow custom types. Obviously, the default capability of handling numerical values was needed as a start. Let's speed up with the some examples.

The library is based on dependency injection concepts and can be highly customized. 
There are 2 main classes: the ```Tokenizer``` and the ```Parser```. Both of them are base classes and adapt to the corresponding interfaces ```ITokenizer``` and ```IParser```. Let's uncover all the potential by giving examples with incremental adding functionality.

## Examples

### Using the DefaultParser

```cs
//This is a simple expression, which uses variables and literals of type double, and the DefaultParser.
double result = (double)App.Evaluate( "-5.0+2*a", new() { { "a", 5.0 } });
Console.WriteLine(result);  //5

//2 variables example (spaces are obviously ignored)
double result2 = (double)App.Evaluate("-a + 500 * b + 2^3", new() { { "a", 5 }, { "b", 1 } });
Console.WriteLine(result2); //503
```

The first example is the same with the example below: the second way uses explicitly the ```DefaultParser```, which can be later overriden in order to use a custom Parser.

```cs
//The example below uses explicitly the DefaultParser.
var app = App.GetParserApp<DefaultParser>();
var parser = app.Services.GetParser();
double result = (double)parser.Evaluate("-5.0+2*a", new() { { "a", 5.0 } });
```

Let's use some functions already defined in the `DefaultParser`

```cs
double result3 = (double)App.Evaluate("cosd(phi)^2+sind(phi)^2", new() { { "phi", 45 } });
Console.WriteLine(result3); //  1.0000000000000002
```

### Adding new functions to the `DefaultParser`

That was the boring stuff, let's start adding some custom functionality. Let's add a custom function ```add3``` that takes 3 arguments. For this purpose, we create a new subclass of ```DefaultParser```. Note that we can add custom logging via dependency injection (some more examples will follow on this). For the moment, ignore the constructor. We assume that the ```add3``` functions sums its 3 arguments with a specific weight.

```cs
private class SimpleFunctionParser : DefaultParser
{
    public SimpleFunctionParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    {
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double[] a = GetDoubleFunctionArguments(functionNode, nodeValueDictionary);

        return functionNode.Text.ToLower() switch
        {
            "add3" => a[0] + 2 * a[1] + 3 * a[2],
            //for all other functions use the base class stuff (DefaultParser)
            _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
        };
    }
}
```

Let's use our first customized `Parser`:

```cs
var parser = App.GetCustomParser<SimpleFunctionParser>();
double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)", new() { { "g", 3 } }); // will return 8 + (5 + 2 * 3 + 3 * 3.0) i.e -> 28
```

### Using custom types

Let's assume that we have a class named ```Item```, which we want to interact with integer numbers and with other ```Item``` objects:

```cs
public class Item
{
    public string Name { get; set; }

    public int Value { get; set; } = 0;

    //we define a custom operator for the type to simplify the evaluateoperator example later
    //this is not 100% needed, but it keeps the code in the CustomTypeParser simpler
    public static Item operator +(int v1, Item v2) =>
        new Item { Name = v2.Name , Value = v2.Value + v1 };
    public static Item operator +(Item v2, int v1) =>
        new Item { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v1, Item v2) =>
        new Item { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    public override string ToString() => $"{Name} {Value}";

}
```
A custom parser that uses custom types should derive from the ```Parser``` class. Because the ```Parser``` class does not assume any type in advance, we should override the ```EvaluateLiteral``` function which is used to parse the integer numbers in the string, In the following example we define the `+` operator, which can take an `Item` object or an `int` for its operands. We also define the `add` function, which assumes that the first argument is an `Item` and the second argument is an `int`. In practice, the Function syntax is usually stricter regarding the type of the arguments, so it is easier to write its implementation:

```cs
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
```

Now we can use the `CustomTypeParser` for parsing our custom expression:

```cs
var parser = App.GetCustomParser<CustomTypeParser>();
Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
    new() {
        {"a", new Item { Name="foo", Value = 3}  },
        {"b", new Item { Name="bar"}  }
    });
Console.WriteLine(result); // foo bar 12
```

### _more examples to follow..._

## The `DefaultParser` Parser

The `DefaultParser` class for the moment accepts the followig operators:
- `+` : plus sign and plus
- `-` : negative sign and minus
- `*` : multiplication
- `/` : division
- `^` : power

and the following functions:

- `abs(x)`: Absolute value
- `acos(x)`: Inverse cosine (in radians)
- `acosd(x)`: Inverse cosine (in degrees)
- `acosh(x)`: Inverse hyperbolic cosine
- `asin(x)`: Inverse sine (in radians)
- `asind(x)`: Inverse sine (in degrees)
- `asinh(x)`: Inverse hyperbolic sine
- `atan(x)`: Inverse tangent (in radians)
- `atand(x)`: Inverse tangent (in degrees)
- `atan2(y,x)`: Inverse tangent (in radians)
- `atan2d(y,x)`: Inverse tangent (in degrees)
- `atanh(x)`: Inverse hyperbolic tangent
- `cbrt(x)`: Cube root
- `cos(x)`: Cosine (x in radians)
- `cosd(x)`: Cosine (x in degrees)
- `cosh(x)`: Hyperbolic cosine
- `exp(x)`: Exponential function (e^x)
- `log(x)` / ln(x): Natural logarithm
- `log10(x)`: Base 10 logarithm
- `log2(x)`: Base 2 logarithm
- `logn(x,n)`: Base n logarithm
- `max(x,y)`: Maximum
- `min(x,y)`: Minimum
- `pow(x,y)`: Power function (x^y)
- `round(x,y)`: Round to y decimal digits
- `sin(x)`: Sine (x in radians)
- `sind(x)`: Sine (x in degrees)
- `sinh(x)`: Hyperbolic sine
- `sqr(x)` / `sqrt(x)`: Square root
- `tan(x)`: Tangent (x in radians)
- `tand(x)`: Tangent (x in degrees)
- `tanh(x)`: Hyperbolic tangent 



