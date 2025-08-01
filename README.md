# ParserLibrary
_No Other Expression Parser, Ever_

### About
I wanted to write my "custom terminal" that used interactive commands with expressions. Other Expression builders used only **numbers** as basic entities which I did not want; this is something too common. I wanted some variables to represent **musical notes/chords**, or even **vectors** and **matrices** and some other to represent numbers.
The only way, was to build an Expression builder that could allow custom types. Obviously, the default capability of handling numerical values was needed as a start.

**The library is frequently updated, so please check again for a newer version and the most recent README after a while 😃.**

The library is based on modern programming tools and can be highly customized. Its basic features are:
- Default support for:
  - Double arithmetic via the `DefaultParser`
  - Complex arithmetic via the `ComplexParser`
  - Vector arithmetic via the `Vector3Parser`
- Logger customization (typically via the `appsettings.json` ).
- Full control of unary and binary operators via configuration files  (typically `appsettings.json`).
- Support for custom data types and/or combination of custom data types with standard data types (such as `int`, `double`).
- Support for custom functions with arbitrary number of arguments. Each argument may be a custom type.

The library is built with modern tools:
- Use of .NET Generic Host (i.e Dependency Inversion/Injection principles, Logging, Configuration) (see [NET Generic Host](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-6.0) for more). All derived Parsers are typically singletons.
- Support for custom loggers (Serilog is implemented by default)

There are 2 main classes: the ```Tokenizer``` and the ```Parser```. Both of them are base classes and adapt to the corresponding interfaces ```ITokenizer``` and ```IParser```. Let's uncover all the potential by giving examples with incrementally added functionality.

### Installation

Via tha Package Manager:
```powershell
Install-Package ParserLibrary
```

Via the .NET CLI
```bat
dotnet add package ParserLibrary
```

### Namespaces

At least the first 2 namespaces below, should be used in order to compile most of the following examples. The other 2 are for more advanced usage (expression trees and tokenizers).

```cs
//use at least these 2 namespaces
using ParserLibrary;
using ParserLibrary.Parsers;

using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;
```

# Simple Parser Examples

## `DefaultParser` examples

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

Let's use some functions already defined in the `DefaultParser`:

```cs
double result3 = (double)App.Evaluate("cosd(ph)^2+sind(ph)^2", new() { { "ph", 45 } });
Console.WriteLine(result3); //  1.0000000000000002
```

...and some constants used in the `DefaultParser`:
```cs
Console.WriteLine(App.Evaluate("5+2*cos(pi)+3*ln(e)")); //will return 5 - 2 + 3 -> 6
```

## `DefaultParser` examples #2 (custom functions)

hat was the boring stuff, let's start adding some custom functionality. Let's add a custom function ```add3``` that takes 3 arguments. For this purpose, we create a new subclass of ```DefaultParser```. Note that we can add custom logging via dependency injection (some more examples will follow on this). For the moment, ignore the constructor. We assume that the ```add3``` functions sums its 3 arguments with a specific weight.
Note that the syntax has been simplified comparing to the previous API versions.

```cs
private class SimpleFunctionParser : DefaultParser
{
    public SimpleFunctionParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : base(logger, options)
    {
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] a = GetDoubleFunctionArguments(args);

        return functionName switch
        {
            "add3" => a[0] + 2 * a[1] + 3 * a[2],
            //for all other functions use the base class stuff (DefaultParser)
            _ => base.EvaluateFunction(functionName, args)
        };
    }
}
```

Let's use our first customized `Parser`:

```cs
var parser = App.GetCustomParser<SimpleFunctionParser>();
double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)", new() { { "g", 3 } }); // will return 8 + (5 + 2 * 3 + 3 * 3.0) i.e -> 28
```

## Single type parsing

If we want to parse an expression that deals with a single data type, then we can avoid the use of creating a custom parser, using the `Parser.Evaluate` function. In the example below, we assume that the expression contains only `int` data types.
```cs
//we use the base Parser here
var parserApp = App.GetParserApp<Parser>();
var parser = parserApp.Services.GetParser();

int result = parser.Evaluate<int>( //returns 860
    "a+f10(8+5) + f2(321+asd*2^2)",
    (s) => int.Parse(s),
    variables:  new () {
        { "a", 8 },
        { "asd", 10 } },
    binaryOperators: new () {
        { "+",(v1,v2)=>v1+v2} ,
        { "*", (v1, v2) => v1 * v2 },
        { "^",(v1,v2)=>(int)Math.Pow(v1,v2)}  },
    funcs1Arg:
    new () {
        { "f10", (v) => 10 * v } ,
        { "f2", (v) => 2 * v }}
    );
```
From the declaration of the function below, we can see that the `Evaluate` function supports functions from up to 3 arguments and the definition of custom operators. As shown in the example above, it is best to use the named parameters syntax.
```cs
 V Evaluate<V>(
        string s,
        Func<string, V> literalParser = null,
        Dictionary<string, V> variables = null,
        Dictionary<string, Func<V, V, V>> binaryOperators = null,
        Dictionary<string, Func<V, V>> unaryOperators = null,

        Dictionary<string, Func<V, V>>? funcs1Arg = null,
        Dictionary<string, Func<V, V, V>>? funcs2Arg = null,
        Dictionary<string, Func<V, V, V, V>>? funcs3Arg = null
        );
```

# Custom Parsers

Any `Parser` that uses custom types should inherit the `Parser` base class. 
Each custom parser should override the methods:
* `Evaluate`: if there is at least one "constant" such as `pi`, which should be defined by default.
* `EvaluateUnaryOperator` : if there is at least one unary operator
* `EvaluateLiteral`: if there is at least one literal value such as `0.421`. In most cases a simple parse function can be called for a `double` or `int`.
* `EvaluateOperator`: if there is at least one binary operator
* `EvaluateFunction`: if there is at least one function.
It is best to understand how to override these functions in the example implementations below. Note that some `Node` functions are used, which are explained later in the text (namely the methods `GetUnaryArgument`, `GetBinaryArguments`, `GetFunctionArguments`).

## Custom parser examples #1:  `ComplexParser`

Another ready to use `Parser` is the `ComplexParser` for complex arithmetic. The application of the `Parser` for `Complex` numbers is a first application of a custom data type (i.e. other that `double`). Let's see the implementation of the `ComplexParser` to clarify how a generic custom parser is implemented:

```cs
using System.Numerics;

namespace ParserLibrary.Parsers;

public class ComplexParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : Parser(logger, options)
{
    protected override object? Evaluate(List<Token> postfixTokens, Dictionary<string, object?>? variables = null)
    {
        variables ??= new Dictionary<string, object?>(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        //we define "constants" if they are not already defined
        if (!variables.ContainsKey("i")) variables.Add("i", Complex.ImaginaryOne);
        if (!variables.ContainsKey("j")) variables.Add("j", Complex.ImaginaryOne);
        if (!variables.ContainsKey("pi")) variables.Add("pi", new Complex(Math.PI, 0));
        if (!variables.ContainsKey("e")) variables.Add("e", new Complex(Math.E, 0));

        return base.Evaluate(postfixTokens, variables);
    }

    protected override object EvaluateLiteral(string s) =>
        double.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    private static Complex GetComplex(object? value)
    {
        if (value is null) return Complex.Zero;
        if (value is double) return new Complex(Convert.ToDouble(value), 0);
        if (value is not Complex) return Complex.Zero;
        return (Complex)value;
    }  
    

    public static (Complex Left, Complex Right) GetComplexBinaryOperands(object? leftOperand, object? rightOperand) => (
        Left: GetComplex(leftOperand),
        Right: GetComplex(rightOperand)
    );

    public static Complex GetComplexUnaryOperand(object? operand) => GetComplex(operand);

    public static Complex[] GetComplexFunctionArguments(object?[] args) =>
        [.. args.Select(GetComplex)];

    #endregion

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        Complex op = GetComplexUnaryOperand(operand);

        return operatorName switch
        {
            "-" => -op,
            "+" => op,
            _ => base.EvaluateUnaryOperator(operatorName, operand),
        };
    }


    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        var (Left, Right) = GetComplexBinaryOperands( leftOperand,rightOperand);
        return operatorName switch
        {
            "+" => Complex.Add(Left, Right),
            "-" => Left - Right,
            "*" => Left * Right,
            "/" => Left / Right,
            "^" => Complex.Pow(Left, Right),
            _ => base.EvaluateOperator(operatorName, leftOperand,rightOperand),
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        Complex[] a = ComplexParser.GetComplexFunctionArguments(args);
        const double TORAD = Math.PI / 180.0, TODEG = 180.0 * Math.PI;

        return functionName switch
        {
            "abs" => Complex.Abs(a[0]),
            "acos" => Complex.Acos(a[0]),
            "acosd" => Complex.Acos(a[0]) * TODEG,
            "asin" => Complex.Asin(a[0]),
            "asind" => Complex.Asin(a[0]) * TODEG,
            "atan" => Complex.Atan(a[0]),
            "atand" => Complex.Atan(a[0]) * TODEG,
            "cos" => Complex.Cos(a[0]),
            "cosd" => Complex.Cos(a[0] * TORAD),
            "cosh" => Complex.Cosh(a[0]),
            "exp" => Complex.Exp(a[0]),
            "log" or "ln" => Complex.Log(a[0]),
            "log10" => Complex.Log10(a[0]),
            "log2" => Complex.Log(a[0]) / Complex.Log(2),
            "logn" => Complex.Log(a[0]) / Complex.Log(a[1]),
            "pow" => Complex.Pow(a[0], a[1]),
            "round" => new Complex(Math.Round(a[0].Real, (int)a[1].Real), Math.Round(a[0].Imaginary, (int)a[1].Real)),
            "sin" => Complex.Sin(a[0]),
            "sind" => Complex.Sin(a[0] * TORAD),
            "sinh" => Complex.Sinh(a[0]),
            "sqr" or "sqrt" => Complex.Sqrt(a[0]),
            "tan" => Complex.Tan(a[0]),
            "tand" => Complex.Tan(a[0] * TORAD),
            "tanh" => Complex.Tanh(a[0]),
            _ => base.EvaluateFunction(functionName, args),
        };
    }
}
```

Below is an example of usage of the `ComplexParser`:

```cs
using System.Numerics; //needed if we want to further use the result
...
var cparser = App.GetCustomParser<ComplexParser>();

//unless we override the i or j variables, both are considered to correspond to the imaginary unit
//NOTE: because i is used as a variable (internally), the syntax for the imaginary part should be 3*i, NOT 3i
Complex result = (Complex)cparser.Evaluate("(1+3*i)/(2-3*i)"); 
Console.WriteLine(result); // (-0.5384615384615385, 0.6923076923076924)

//another one with a variable (should give the same result) 
Complex result2 = (Complex)cparser.Evaluate("(1+3*i)/b", new() { { "b", new Complex(2,-3)} });
Console.WriteLine(result2); //same result

//and something more "complex", using nested functions: note that the complex number is returned as a string in the form (real, imaginary) 
Console.WriteLine(cparser.Evaluate("cos((1+i)/(8+i))")); //(0.9961783779071353, -0.014892390041785901)
Console.WriteLine(cparser.Evaluate("round(cos((1+i)/(8+i)),4)")); //(0.9962, -0.0149)

Console.WriteLine(cparser.Evaluate("round(exp(i*pi),8)")); //(-1, 0)  (Euler is correct!)
```
## Custom parser examples #2:  `Vector3Parser`

`Vector3Parser` is the corresponding parser for vector arithmetic. The `Vector3` is also included in the `System.Numerics` namespace. The implementation of the `Vector3Parser` is similar to the implementation of the `ComplexParser`. The same methods from the `Parser` base class are overriden.

```cs
namespace ParserLibrary.Parsers;

using ParserLibrary.Tokenizers;
using System.Numerics;

ppublic class Vector3Parser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : Parser(logger, options)
{
    protected override object? Evaluate(List<Token> postfixTokens, Dictionary<string, object?>? variables = null)
    {
        variables ??= new Dictionary<string, object?>(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        if (!variables.ContainsKey("pi")) variables.Add("pi", DoubleToVector3((float)Math.PI));
        if (!variables.ContainsKey("e")) variables.Add("e", DoubleToVector3((float)Math.E));

        if (!variables.ContainsKey("ux")) variables.Add("ux", Vector3.UnitX);
        if (!variables.ContainsKey("uy")) variables.Add("uy", Vector3.UnitY);
        if (!variables.ContainsKey("uz")) variables.Add("uz", Vector3.UnitZ);

        return base.Evaluate(postfixTokens, variables);
    }

    protected override object EvaluateLiteral(string s) =>
        float.Parse(s, CultureInfo.InvariantCulture);


    #region Auxiliary functions to get operands

    public static Vector3 DoubleToVector3(object arg)
        => new(Convert.ToSingle(arg), Convert.ToSingle(arg), Convert.ToSingle(arg));

    public static bool IsNumeric(object arg) =>
           arg is double || arg is int || arg is float;

    public static Vector3 GetVector3(object? arg)
    {
        if (arg is null) return Vector3.Zero;
        if (IsNumeric(arg)) return DoubleToVector3(arg);
        if (arg is Vector3 v) return v;
        return Vector3.Zero;
    }

    public static Vector3 GetVector3UnaryOperand(object? operand) =>
        GetVector3(operand);

    public static (Vector3 Left, Vector3 Right) GetVector3BinaryOperands(object? leftOperand, object? rightOperand) => (
        Left: GetVector3(leftOperand),
        Right: GetVector3(rightOperand)
    );

    public static Vector3[] GetVector3FunctionArguments(object?[] args) =>
        [.. args.Select(GetVector3)];

    #endregion

    protected override object? EvaluateUnaryOperator(string operatorName, object? operand)
    {
        Vector3 op = GetVector3UnaryOperand(operand);

        return operatorName switch
        {
            "-" => -op,
            "+" => op,
            "!" => Vector3.Normalize(op),
            _ => base.EvaluateUnaryOperator(operatorName, operand)
        };
    }


    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        (Vector3 left, Vector3 right) = GetVector3BinaryOperands(leftOperand,rightOperand);

        return operatorName switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "^" => Vector3.Cross(left, right),
            "@" => Vector3.Dot(left, right),
            _ => base.EvaluateOperator(operatorName, leftOperand,rightOperand)
        };
    }

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        Vector3[] a = GetVector3FunctionArguments(args);

        return functionName switch
        {
            "abs" => Vector3.Abs(a[0]),
            "cross" => Vector3.Cross(a[0], a[1]),
            "dot" => Vector3.Dot(a[0], a[1]),
            "dist" => Vector3.Distance(a[0], a[1]),
            "dist2" => Vector3.DistanceSquared(a[0], a[1]),
            "lerp" => Vector3.Lerp(a[0], a[1], a[2].X),
            "length" => a[0].Length(),
            "length2" => a[0].LengthSquared(),
            "norm" => Vector3.Normalize(a[0]),
            "sqr" or "sqrt" => Vector3.SquareRoot(a[0]),
            "round" => new Vector3(
                            (float)Math.Round(a[0].X, (int)a[1].X),
                            (float)Math.Round(a[0].Y, (int)a[1].X),
                            (float)Math.Round(a[0].Z, (int)a[1].X)),
            _ => base.EvaluateFunction(functionName, args),
        };
    }

}
```


Let's see some example usage too:

```cs
using System.Numerics; //needed if we want to further use the result
...
var vparser = App.GetCustomParser<Vector3Parser>();

Vector3 v1 = new Vector3(1, 4, 2),
    v2 = new Vector3(2, -2, 0);

Console.WriteLine(vparser.Evaluate("!(v1+3*v2)", //! means normalize vector
   new() { { "v1", v1 }, { "v2", v2 } })); //<0.92717266. -0.26490647. 0.26490647>

Console.WriteLine(vparser.Evaluate("10 + 3 * v1^v2", // ^ means cross product
   new() { { "v1", v1 }, { "v2", v2 } })); //<22. 22. -20>


Console.WriteLine(vparser.Evaluate("v1@v2", // @ means dot product
   new() { { "v1", v1 }, { "v2", v2 } })); //-6

Console.WriteLine(vparser.Evaluate("lerp(v1, v2, 0.5)", // lerp (linear combination of vectors)
   new() { { "v1", v1 }, { "v2", v2 } })); //<1.5, 1. 1>

Console.WriteLine(vparser.Evaluate("6*ux -12*uy + 14*uz")); //<6. -12. 14>
```
## Custom parser examples #3: `CustomTypeParser` and the `CustomTypeStatefulParser`

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
    public CustomTypeParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : base(logger, options)
    { }


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

### `CustomTypeStatefulParser` and the `NodeValueDictionary`

We can see from the example of the `CustomTypeParser`, that each function carries a `nodeValueDictionary` argument. An expected remark, is why this happens. All `Parser` subclasses are expected to be used as singletons, so to guarantee the thread-safe operations they are state-less. Being stateless, means that if we want a lot of parallel requests to be handled by the same `Parser` instance, then each expression has to store its own version of `nodeValueDictionary`. The latter is literally a `Dictionary` which stores the values, which have been evaluated at each stage of the parsing.

In case, we want one of the following 2 cases:
* a `Parser` instance per expression
* a `Parser` that handles only non-parallel requests (such as the case of a terminal console),

then the `NodeValueDictionary` could possibly be stored as an internal field and simplify the subclass definition. That's why the library contains another Parser variant named `StatefulParser`. Subclasses of `StatefulParser` (which implement the `IStatefulParser` interface), are typically created with transient scope (not singleton), in order to avoid any conflict. All `StatefulParser` instances come with state and have the `nodeValueDictionary` as an internal protected member. The example below, shows how we would implement a `StatefulParser` for the same `Item`. The syntax of the `CustomTypeStatefulParser` is simpler than the syntax of the `CustomTypeParser`, because it practically omits passing the `nodeValueDictionary` for each function call. Let's see how:

```cs
public class CustomTypeStatefulParser : StatefulParser
{
    public CustomTypeStatefulParser(ILogger<StatefulParser> logger, IOptions<TokenizerOptions> options, string expression) :
        base(logger, options, expression)
    { }

    //we assume that literals are integer numbers only
    protected override object EvaluateLiteral(string s) => int.Parse(s);

    protected override object? EvaluateOperator(string operatorName, object? leftOperand, object? rightOperand)
    {
        if (operatorName == "+")
        {
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
        //MODIFIED: used the CaseSensitive from the options in the configuration file. The options are retrieved via dependency injection.
        string actualFunctionName = _options.CaseSensitive ? functionName : functionName.ToLower();
        
        return actualFunctionName switch
        {
            "add" => (Item)args[0]! + (int)args[1]!,
            _ => base.EvaluateFunction(functionName, args)
        };
    }
}
```

In order to use the `StatefulParser` we call the `GetCustomStatefulParser` instead of the `GetCustomParser` function as shown below. Note that the `StatefulParser` requires the expression to be provided during construction. Other than that, the syntax of the `Evaluate` function call is simplified as it doesn't require the expression parameter:

```cs
var parser = App.GetCustomStatefulParser<CustomTypeStatefulParser>("a + add(b,4) + 5");

Item result = (Item)parser.Evaluate(
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

  "tokenizer":  {
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
        },
        {
          "name": "@",
          "priority": 4
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


If you want to extend your own `IHostBuilder` then this is easily feasible via the `AddParserLibrary` extension method. This includes the `ITokenizer`, the `IParser` and the `TokenizerOptions`. Examples of using the extension methods are given below:
```cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParserLibrary;
...
 IHostBuilder builder = Host.CreateDefaultBuilder()
   ...
   .ConfigureServices((context, services) =>
    {
        services
        //NOTE: TParser should be one of the derived parsers such as DefaultParser
        .AddParserLibrary<TParser>(context) //extension method. 
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
- `+`: plus sign (unary) and plus (binary)
- `-`: negative sign (unary) and minus (binary)
- `*`: multiplication
- `/`: division
- `^`: power

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
- `pow(x,y): Power function (x^y)
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
- `+`: plus sign (unary) and plus (binary)
- `-`: negative sign (unary) and minus (binary)
- `*`: multiplication
- `/`: division
- `^`: power

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
- `i` , `j`: the imaginary unit (see [imaginary unit](https://en.wikipedia.org/wiki/Imaginary_unit))
- `pi`: the number π (see [π](https://en.wikipedia.org/wiki/Pi))
- `e`: the Euler's number (see [e](https://en.wikipedia.org/wiki/E_(mathematical_constant))) 

## `Vector3Parser`

The `Vector3Parser` class for the moment accepts the followig operators:
- `+`: plus sign (unary) and plus (binary)
- `-`: negative sign (unary) and minus (binary)
- `*`: multiplication (element-wise)
- `/`: division (element-wise)
- `^`: cross product (e.g. v1 ^ v2)
- `@`: dot vector (e.g. v1 @ v2)
- `!`: normalize vector (e.g. !v1)

and the following functions:

- `abs(v)`: Absolute value
- `cross(v1,v2)`: Cross product (same result with `^`)
- `dot(v1,v2)`: Dot product (same result with `@`)
- `dist(v1,v2)`: Distance
- `dist2(v1,v2)`: Distance squared
- `lerp(v1,v2,f)`: Linear combination of v1, v2
- `length(v)`: Vector length
- `length2(v)`: Vector length squared
- `norm(v)`: Normalize (same result with `!`)
- `sqr(v)` / `sqrt(v)`: Square root
- `round(v,f)`: Round to f decimal digits

The following constants are also defined _unless_ the same names are overriden by the `variables` dictionary argument when calling the `Evaluate` function:
- `pi`: the number π (see [π](https://en.wikipedia.org/wiki/Pi))
- `e`: the Euler's number (see [e](https://en.wikipedia.org/wiki/E_(mathematical_constant))) 
- `ux`: the unit vector X
- `uy`: the unit vector Y
- `uz`: the unit vector Z 

## The Expression Tree

The project is based on Expression binary trees. The classes `Node<T>`, `NodeBase` and `Tree` (all 3 in namespace `ParserLibrary.ExpressionTree`) are comprising everything we need about binary trees. In fact due to the fact that `Node<T>` is generic, we can use it for other uses as well. Each `Parser` uses a `Tokenizer` to facilitate any parsing. For the example below, we get the in-order, pre-order and post-order Nodes for the root node of the expression tree. For each `Tree` or a specific `Node`, we can print to the console a depiction of the binary tree. Visualizing the epressing tree is great for understanding the operations priorities
```cs
string expr = "a+tan(8+5) + sin(321+afsd*2^2)";
var parser = App.GetDefaultParser();
var tokenizer = app.Services.GetTokenizer();
var tree = parser.GetExpressionTree(expr);
Console.WriteLine("Post order traversal: " + string.Join(" ", tree.Root.PostOrderNodes().Select(n => n.Text)));
Console.WriteLine("Pre order traversal: " + string.Join(" ", tree.Root.PreOrderNodes().Select(n => n.Text)));
Console.WriteLine("In order traversal: " + string.Join(" ", tree.Root.InOrderNodes().Select(n => n.Text)));
tree.Print(withSlashes:false) ;
```

The code above prints the following to the Console:
```
Post order traversal: a 8 5 + tan + 321 afsd 2 2 ^ * + sin +
Pre order traversal: + + a tan + 8 5 sin + 321 * afsd ^ 2 2
In order traversal: a + tan 8 + 5 + sin 321 + afsd * 2 ^ 2


      +
   ┌──└───┐
   +     sin
  ┌└─┐     └┐
  a tan     +
      └┐  ┌─└─┐
       + 321  *
      ┌└┐   ┌─└┐
      8 5 afsd ^
              ┌└┐
              2 2
```
## `Tree<T>`
The `Tree<T>` class contains the following important members:
* `Root [Node<T>]`: The Root Node of the tree. This the only node in the `Tree` which has no parent nodes.
* `Print [void]`: Prints the Tree to the console using 2 available variations controlled by the argument `withSlashes`.
* `Count [int]`: The total number of nodes in the tree.
* `GetHeight() [int]`: The height of the binary tree. For example a tree with a single root node and 2 leafs has a height of 1.
* `GetLeafNodesCount() [int]`: The number of leaf nodes.

## `NodeBase`
The `NodeBase` class contains the core of the binary tree functionality:
* `Text [string]`: The text representation of the node
* `Left [NodeBase]`: The left child node
* `Right [NodeBase]`: The right child node
* `PreOrderNodes [IEnumerable<NodeBase>)`: All nodes starting from the current node, in pre-order arrangement 
* `PostOrderNodes [IEnumerable<NodeBase>)`: All nodes starting from the current node, in post-order arrangement
* `InOrderNodes [IEnumerable<NodeBase>)`: All nodes starting from the current node, in in-order arrangement

## `Node<T>`
The `Node<T>` inherits `NodeBase` and includes some additional members which are expression-oriented:
* `Value<T>`: The value of the node. The inherited `Text` property should be a string representation of this value.
To facilitate the retrieval of child nodes depending on the type of each token, some methods of the `Node<T>` are very practical:
* `GetUnaryArgument [object]`: Retrieves the value of the child node, assuming that the token represents a unary operator (such as unary `-`).
* `GetBinaryArguments [(object LeftOperand, object RightOperand)]`: Retrieves the value of the two operand child nodes, assuming that the token represents a binary operator (such as `*` and `^`).
* `GetFunctionArguments [(object[]]`: Retrieves the values of the function argument nodes, assuming that the token represents a function token operator (such as `sin`).
Note, that we are not using any generic types for the node values. The array of objects allow the single returned array to return instances of different daa types.


### _more documentation to follow **soon**..._





