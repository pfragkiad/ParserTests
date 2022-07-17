# ParserLibrary
_No Other Expression Parser, Ever_

### About / How it began
I wanted to write my "custom terminal" that used interactive commands with expressions. Other Expression builders used only **numbers** as basic entities which I did not want; this is something too common. I wanted some variables to represent **musical notes/chords**, or even **vectors** and **matrices** and some other to represent numbers.
The only way, was to build an Expression builder that could allow custom types. Obviously, the default capability of handling numerical values was needed as a start.

The library is based on modern programming tools and can be highly customized. Its basic features:
- Default support for double arithmetic (via `DefaultParser`), complex arithmetic (via `ComplexParser`).
- Logger customization (typically via the `appsettings.json` ).
- Full control of unary and binary operators via configuration files  (typically `appsettings.json`).
- Support for custom data types and/or combination of custom data types with standard data types (such as `int`, `double`).
- Support for custom functions with arbitrary number of arguments. Each one of these may be a custom type.

Built with modern tools:
- .NET 6.0
- Use of .NET Generic Host (i.e Dependency Inversion/Injection principles, Logging, Configuration) (see [NET Generic Host](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-6.0) for more). All derived Parsers are typically singletons.
- Support for custom loggers (Serilog is implemented by default)

There are 2 main classes: the ```Tokenizer``` and the ```Parser```. Both of them are base classes and adapt to the corresponding interfaces ```ITokenizer``` and ```IParser```. Let's uncover all the potential by giving examples with incrementally added functionality.

# Examples

## Using the DefaultParser

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
double result3 = (double)App.Evaluate("cosd(ph)^2+sind(ph)^2", new() { { "ph", 45 } });
Console.WriteLine(result3); //  1.0000000000000002
```

...and some constants used in the `DefaultParser`
```cs
Console.WriteLine(App.Evaluate("5+2*cos(pi)+3*ln(e)")); //will return 5 - 2 + 3 -> 6
```

## Adding new functions to the `DefaultParser`

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
Another ready to use `Parser` is the `ComplexParser` for complex arithmetic. In fact, the application of the `Parser` for `Complex` numbers is a first application of a custom data type (i.e. other that `double`). Let's see an example (`Complex` belongs to the `System.Numerics` namespace):

```cs
using System.Numerics; //needed if we want to further use the result
...
var cparser = App.GetCustomParser<ComplexParser>();
//unless we override the i or j variables, both are considered to correspond to the imaginary unit
Complex result = (Complex)cparser.Evaluate("(1+3*i)/(2-3*i)"); 
Console.WriteLine(result); // (-0.5384615384615385, 0.6923076923076924)
Complex result2 = (Complex)cparser.Evaluate("(1+3*i)/b", new() { { "b", new Complex(2,-3)} });
Console.WriteLine(result2); //same result
```

## Using custom types

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
A custom parser that uses custom types derives from the ```Parser``` class. Because the ```Parser``` class does not assume any type in advance, we should override the ```EvaluateLiteral``` function which is used to parse the integer numbers in the string. In the following example we define the `+` operator, which can take an `Item` object or an `int` for its operands. We also define the `add` function, which assumes that the first argument is an `Item` and the second argument is an `int`. In practice, the Function syntax is usually stricter regarding the type of the arguments, so it is easier to write its implementation:

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
## _more examples to follow **soon**..._

# Customizing ParserLibrary

## `appsettings.json` configuration file

The `appsettings.json` configuration file is crucial, when the user wants to have precise control over the tokenizer and the logger as well.
The library is configured to use Serilog for debugging and informational purposes. The Serilog section (see [Serilog configuration](https://github.com/serilog/serilog-settings-configuration) for more) typically can be configured to output to the Console and/or to an external file. In order to show less messages in the case below, we can use `"Information"` instead of `"Debug"` for the `Console` output. The logger can be accessed via the `_logger` field in every `Parser` subclass, so we can output debug/informational/critical messages to the screen/to a file in a controlled manner. The `_logger` field is of type `ILogger`, so Serilog is not the only type of logger that can be used (although it is recommended).
The tokenizer options include the following properties:
 - `caseSensitive` : if false, then the tokenizer/parser should be case insensitive
 - `tokenPatterns` : this includes the regular expression patterns or simple string characters for identifying any token
   - `identifier` : regular expression to identify variable and function names as tokens
   - `literal` : regular expresssion to identify all literal -typically numeric- values
   - `openParenthesis`, `closeParenthesis`, `argumentSeparator` : the characters which correspond to the parenthesis pair and the argument separator
   - `unary` : the unary array defines all unary operators. The priority of unary operator priority is in general higher than the binary operators
   - `operators` : the operators array defines all binary operators. All binary operators are left-to-right by default except if specified otherwise (just like the exponent operator `'^'`)
Operators with higher `priority` have higher precedence for the calculations. The priority is overriden as always via the use of parentheses, which are identified as defined above.

```json
{
  "Serilog": {
    "MinimumLevel": "Debug",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "restrictedToMinimumLevel": "Debug"
        }
      }
    ]
  },

  "tokenizer": {
    "version": "1.0",
    "caseSensitive": false,
    "tokenPatterns": {
      "identifier": "[A-Za-z_]\\w*",
      "literal": "\\b(?:\\d+(?:\\.\\d*)?|\\.\\d+)\\b",
      "openParenthesis": "(",
      "closeParenthesis": ")",
      "argumentSeparator": ",",

      "unary": [
        {
          "name": "-",
          "priority": 3,
          "prefix": true
        },
        {
          "name": "+",
          "priority": 3,
          "prefix": true
        },
        {
          "name": "!",
          "priority": 3,
          "prefix": true
        },
        {
          "name": "%",
          "priority": 3,
          "prefix": false
        },
        {
          "name": "*",
          "priority": 3,
          "prefix": false
        }
      ],
      "operators": [
        {
          "name": ",",
          "priority": 0
        },
        {
          "name": "+",
          "priority": 1
        },
        {
          "name": "-",
          "priority": 1
        },
        {
          "name": "*",
          "priority": 2
        },
        {
          "name": "/",
          "priority": 2
        },
        {
          "name": "^",
          "priority": 4,
          "lefttoright": false
        }
      ]
    }
  }
}

```

The `appsettings.json` file should exist in the same folder with the executable, so be sure that the file is set to be copied to the output directory. For example, inside the project file, the following block should be included:

```xml
<ItemGroup>
    <EmbeddedResource Include="appsettings.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </EmbeddedResource>
</ItemGroup>
```

Note that we are not bound to use the specific name for the configuration file. For example, we might want to keep the `appsettings.json` file for the logger configuration, and have another file `parsersettings.json` for the tokenizer (which should be also in the same directory with the executable). In order to define the `parsersettings.json` file, we define it as an argument when retrieving the `IHost` app instance, or immediately the `IParser` parser instance via the following calls:

```cs
var app = App.GetParserApp<DefaultParser>("parsersettings.json");
var parser = app.Services.GetParser();

//or to immediately get the parser

var parser2 = App.GetCustomParser<DefaultParser>("parsersettings.json");
```

Note, that in both cases above, the `appsettings.json` is also read (if found). The `parsersettings.json` file has higher priority, in case there are some conflicting options.

An example of using the internal fields `_options` of type `TokenizerOptions` and `_logger` of type `ILogger` can be shown below, by modifying the `CustomTypeParser` slightly modifying the example above:
```cs
...
protected override object EvaluateOperator(Node<Token> operatorNode, Dictionary<Node<Token>, object> nodeValueDictionary)
{
    (object LeftOperand, object RightOperand) = operatorNode.GetBinaryArguments(nodeValueDictionary);

    if (operatorNode.Text == "+")
    {
        //ADDED:
        _logger.LogDebug("Adding with + operator ${left} and ${right}",LeftOperand,RightOperand);

        if (LeftOperand is Item && RightOperand is Item)
            return (Item)LeftOperand + (Item)RightOperand;

        return LeftOperand is Item ?  (Item)LeftOperand + (int)RightOperand : (int)LeftOperand + (Item)RightOperand;
    }

    return base.EvaluateOperator(operatorNode, nodeValueDictionary);
}

protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
{
    var a = functionNode.GetFunctionArguments(nodeValueDictionary);

    //return functionNode.Text switch
    //MODIFIED: use the CaseSensitive property from the options in the configuration files
    return _options.CaseSensitive ? functionNode.Text.ToLower() : functionNode.Text switch
    {
        "add" => (Item)a[0] + (int)a[1],
        _ => base.EvaluateFunction(functionNode, nodeValueDictionary)
    };
}
```

If the user wants to extend his own `IHostBuilder` then this is easily feasible via the `AddParserLibrary` extension method (the `ParserLibrary` namespace should be used within the file). This includes the `ITokenizer`, the `IParser` and the `TokenizerOptions`. Examples of using the extension methods are given below:
```cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
...
 IHostBuilder builder = Host.CreateDefaultBuilder()
   ...
   .ConfigureServices((context, services) =>
    {
        services
        .AddParserLibrary<TParser>(context) //extension method
        ...
        ;
    })
    ...
var app = builder.Build();
...

var parser1 = app.Services.GetService<IParser>(); 
//or
var parser2 = app.Services.GetParser(); //extension method

//sample calls if we want to retrieve instances of Tokenizer and TokenizerOptions outside a subclassed Parser
var tokenizer1 = app.Services.GetService<ITokenizer>();
//or
var tokenizer2 = app.Services.GetTokenizer(); //extension method

var tokenizerOptions = app.Services.GetService<IOptions<TokenizerOptions>>().Value;
```

# Parsers

Every `Parser` subclass adapts to the `IParser` interface and typically every `Parser` derives from the `Parser` base class.
All derived Parsers use parenthesis pairs (`(`, `)`) by default to override the operators priority. The priority of the operators is internally defined in the `DefaultParser`. A custom `Parser` can override the default operator priority and use other than the common operators using an external `appsettings.json` file, which will be analyzed in later examples.

## `DefaultParser`

The `DefaultParser` class for the moment accepts the followig operators:
- `+` : plus sign (unary) and plus (binary)
- `-` : negative sign (unary) and minus (binary)
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
- `log(x)` / `ln(x)`: Natural logarithm
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

The following constants are also defined _unless_ the same names are overriden by the `variables` dictionary argument when calling the `Evaluate` function:
- `pi` : the number π (see [π](https://en.wikipedia.org/wiki/Pi))
- `e` : the Euler's number (see [e](https://en.wikipedia.org/wiki/E_(mathematical_constant))) 
- `phi` : the golden ratio φ (see [φ](https://en.wikipedia.org/wiki/Golden_ratio))

## `ComplexParser`

The `ComplexParser` class for the moment accepts the followig operators:
- `+` : plus sign (unary) and plus (binary)
- `-` : negative sign (unary) and minus (binary)
- `*` : multiplication
- `/` : division
- `^` : power

and the following functions:

- `abs(z)`: Absolute value
- `acos(z)`: Inverse cosine (in radians)
- `acosd(z)`: Inverse cosine (in degrees)
- `asin(z)`: Inverse sine (in radians)
- `asind(z)`: Inverse sine (in degrees)
- `atan(z)`: Inverse tangent (in radians)
- `atand(z)`: Inverse tangent (in degrees)
- `cos(z)`: Cosine (z in radians)
- `cosd(z)`: Cosine (z in degrees)
- `cosh(z)`: Hyperbolic cosine
- `exp(z)`: Exponential function (e^z)
- `log(z)` / `ln(z)`: Natural logarithm
- `log10(z)`: Base 10 logarithm
- `log2(z)`: Base 2 logarithm
- `logn(z,n)`: Base n logarithm
- `pow(z,y)`: Power function (z^y)
- `round(z,y)`: Round to y decimal digits
- `sin(z)`: Sine (z in radians)
- `sind(z)`: Sine (z in degrees)
- `sinh(z)`: Hyperbolic sine
- `sqr(z)` / `sqrt(z)`: Square root
- `tan(z)`: Tangent (z in radians)
- `tand(z)`: Tangent (z in degrees)
- `tanh(z)`: Hyperbolic tangent 

The following constants are also defined _unless_ the same names are overriden by the `variables` dictionary argument when calling the `Evaluate` function:
- `i` , `j` : the imaginary unit (see [imaginary unit](https://en.wikipedia.org/wiki/Imaginary_unit))
- `pi` : the number π (see [π](https://en.wikipedia.org/wiki/Pi))
- `e` : the Euler's number (see [e](https://en.wikipedia.org/wiki/E_(mathematical_constant))) 

### _more documentation to follow **soon**..._



