# ParserLibrary
_No Other Expression Parser, Ever_

### About
I wanted to create my own custom terminal that supported interactive commands with expressions. Other expression builders I found only used numbers as the basic entities, which I didn’t want—this felt too common. I wanted variables that could represent chords, vectors, matrices, and, of course, numbers.
The only way to build an optimized version of what I wanted, was to build an expression builder that inherently support custom types. Naturally, the ability to handle numerical values was needed as a starting point.

**The library is frequently updated, so please check back for newer versions and the most recent README.**

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

At least the first 2 namespaces below, should be used in order to compile most of the following examples. 

```cs
//use at least these 2 namespaces
using ParserLibrary;
using ParserLibrary.Parsers;

using ParserLibrary.Parsers.Common;
using ParserLibrary.Tokenizers;
using ParserLibrary.ExpressionTree;
```

# Simple Parser Examples

## `DefaultParser` examples

```cs
//This is a simple expression, which uses variables and literals of type double, and the DefaultParser.
double result = (double)ParserApp.Evaluate( "-5.0+2*a", new() { { "a", 5.0 } });
Console.WriteLine(result);  //5

//2 variables example (spaces are obviously ignored)
double result2 = (double)ParserApp.Evaluate("-a + 500 * b + 2^3", new() { { "a", 5 }, { "b", 1 } });
Console.WriteLine(result2); //503
```

All examples below are equivalent; the first and the second examples explicitly retrieve the ```DefaultParser``` instance, which contains default arithmetic functionality.

```cs
//The example below uses explicitly the DefaultParser. Use one of the alternatives below:

//v1
var app = ParserApp.GetCommonsApp(); //returns an IHost with the common parsers registered
var parser = app.Services.GetParser("Default");
double result = (double)parser.Evaluate("-5.0+2*a", new() { { "a", 5.0 } });

//v2
var parser = ParserApp.GetDefaultParser(); //returns an IHost with the common parsers registered
double result = (double)parser.Evaluate("-5.0+2*a", new() { { "a", 5.0 } });

//v3
double result = (double)ParserApp.Evaluate("-5.0+2*a", new() { { "a", 5.0 } });
```

Let's use some functions already defined in the `DefaultParser`:

```cs
double result3 = (double)ParserApp.Evaluate("cosd(ph)^2+sind(ph)^2", new() { { "ph", 45 } });
Console.WriteLine(result3); //  1.0000000000000002
```

...and some constants used in the `DefaultParser`:
```cs
Console.WriteLine(ParserApp.Evaluate("5+2*cos(pi)+3*ln(e)")); //will return 5 - 2 + 3 -> 6
```

## `DefaultParser` examples #2 (custom functions)

That was the boring stuff, let's start adding some custom functionality. Let's add a custom function ```add3``` that takes 3 arguments. For this purpose, we create a new subclass of ```DefaultParser```. Note that we can add custom logging via dependency injection (some more examples will follow on this). For the moment, ignore the constructor. We assume that the ```add3``` functions sums its 3 arguments with a specific weight.

```cs
public class SimpleFunctionParser(ILogger<ParserBase> logger, IOptions<TokenizerOptions> options) : DefaultParser(logger, options)
{

    protected override object? EvaluateFunction(string functionName, object?[] args)
    {
        double[] d = GetDoubleFunctionArguments(args);

        return functionName switch
        {
            "add3" => d[0] + 2 * d[1] + 3 * d[2],

            //for all other functions use the base class stuff (DefaultParser)
            _ => base.EvaluateFunction(functionName, args)
        };
    }

}

```

Let's use our first customized `Parser`. There are many ways to retrieve the parser depending on the project needs. See some variants below:

```cs
//v1
var app = ParserApp.GetParserApp<SimpleFunctionParser>();
var parser = app.GetParser();
// will return 8 + (5 + 2 * 3 + 3 * 3.0) i.e -> 28
double result = (double)parser.Evaluate("8 + add3(5.0,g,3.0)", new() { { "g", 3 } });

//v2 (assuming you want to explicitly configure the host)
var app = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddParser<SimpleFunctionParser>(context);
    }).Build();
var parser = app.GetParser();

//v3 (assuming you want to add multiple parsers, a keyed version is suitable)
var app = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddParser<SimpleFunctionParser>("simple", TokenizerOptions.Default);
        services.AddParser<ComplexParser>("complex", TokenizerOptions.Default);
    }).Build();
var parser = app.GetParser("simple");
...
```

## Single type parsing

If we want to parse an expression that deals with a single data type, then we can avoid the use of creating a custom parser, using the generic `Evaluate<T>` function.
In the example below, we assume that the expression contains only `int` data types. For this case the `CoreParser` can be used. 
The `CoreParser` can be created in many ways:

```cs
//v1
IParser parser = ParserApp.GetCoreParser();

//v2 (explicitly creating the parser with a new host)
IHost app = ParserApp.GetParserApp<CoreParser>();
IParser parser = app.GetParser();

//v3 (use the 'commons' app (host) which includes common parsers: "Core", "Default" or "Double", "Complex", "Vector3")
IHost app = ParserApp.GetCommonsApp();
IParser parser = app.GetParser("Core");
```

After retrieving the parser based on preference, the `Evaluate(T)` method is used as shown below. In this example, we define custom functions `f10` and `f2`, which multiply their argument by 10 and 2 respectively. We also define the operators `+`, `*` and `^` (power). The variables `a` and `asd` are also defined. The expression is evaluated to an integer value.

```cs
//we use the core Parser here
IParser parser = ParserApp.GetCoreParser();

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
        string expression,
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

Any `Parser` that uses custom types should inherit the `CoreParser` base class. 
Each custom parser should override the members:
* `Constants`: if there is at least one "constant" such as `pi`, which should be defined by default.
* `EvaluateLiteral`: if there is at least one literal value such as `0.421`. In most cases a simple parse function can be called for a `double` or `int`.
* `EvaluateUnaryOperator` : if there is at least one unary operator
* `EvaluateOperator`: if there is at least one binary operator
* `EvaluateFunction`: if there is at least one function.
It is best to understand how to override these functions in the example implementations below.

## Custom parser examples #1:  `ComplexParser`

Another ready to use `Parser` is the `ComplexParser` for complex arithmetic. The application of the `Parser` for `Complex` numbers is a first application of a custom data type (i.e. other that `double`). Let's see the implementation of the `ComplexParser` to clarify how a generic custom parser is implemented:

```cs
using System.Numerics;

namespace ParserLibrary.Parsers;

public class ComplexParser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : CoreParser(logger, options)
{
 public override Dictionary<string, object?> Constants => 
        new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
        {
            { "i", Complex.ImaginaryOne },
            { "j", Complex.ImaginaryOne },
            { "pi", new Complex(Math.PI, 0) },
            { "e", new Complex(Math.E, 0) }
        };

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
        Complex[] a = GetComplexFunctionArguments(args);
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
var cparser = ParserApp.GetComplexParser();

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

public class Vector3Parser(ILogger<Parser> logger, IOptions<TokenizerOptions> options) : CoreParser(logger, options)
{
   public override Dictionary<string, object?> Constants =>
        new(_options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
        {
            { "pi", DoubleToVector3((float)Math.PI) },
            { "e", DoubleToVector3((float)Math.E) },
            { "ux", Vector3.UnitX },
            { "uy", Vector3.UnitY },
            { "uz", Vector3.UnitZ }
        };

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
var vparser = ParserApp.GetVector3Parser();

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
## Custom parser examples #3: `ItemParser` and the `ItemStatefulParser`

Let's assume that we have a class named `Item`, which we want to interact with integer numbers and with other `Item` objects:

```cs
public class Item
{
    public string Name { get; set; }

    public int Value { get; set; } = 0;

    //we define a custom operator for the type to simplify the evaluateoperator example later
    //this is not 100% needed, but it keeps the code in the custom parser simpler

    public static Item operator +(int v1, Item v2) =>
        new Item { Name = v2.Name , Value = v2.Value + v1 };
    public static Item operator +(Item v2, int v1) =>
        new Item { Name = v2.Name, Value = v2.Value + v1 };

    public static Item operator +(Item v1, Item v2) =>
        new Item { Name = $"{v1.Name} {v2.Name}", Value = v2.Value + v1.Value };

    public override string ToString() => $"{Name} {Value}";

}
```

A custom parser that uses custom types derives from the `CoreParser` class. Because the `CoreParser` class does not assume any type in advance, we should override the `EvaluateLiteral` function which is used to parse the integer numbers in the string. In the following example we define the `+` operator, which can take an `Item` object or an `int` for its operands. We also define the `add` function, which assumes that the first argument is an `Item` and the second argument is an `int`. In practice, the Function syntax is usually stricter regarding the type of the arguments, so it is easier to write its implementation:

```cs
public class ItemParser(ILogger<CoreParser> logger, IOptions<TokenizerOptions> options) : CoreParser(logger, options)

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

Now we can use the `ItemParser` for parsing our custom expression. To create the parser we can use many alternatives:

```cs
//v1 (implicitly create host too) 
var parser =  ParserApp.GetParser<ItemParser>();

//v2 (explicitly create host)
var parser = ParserApp.GetParserApp<ItemParser>().GetParser();

//v3 (explicitly add to existing host) 
var host  = Host.CreateDefaultBuilder()
.ConfigureServices((context, services) =>
    {
        services.AddParser<ItemParser>(context); 
        ...
    }).Build();
var parser = host.GetParser();

//v4 (keyed version, if multiple parsers are used)
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddParser<ItemParser>("item", TokenizerOptions.Default);
    }).Build();
var parser = host.GetParser("item");

```

After retrieving the parser, we can evaluate an expression as shown below:


```cs
var parser = ParserApp.GetParser<ItemParser>();

Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
    new() {
        {"a", new Item { Name="foo", Value = 3}  },
        {"b", new Item { Name="bar"}  }
    });
Console.WriteLine(result); // foo bar 12
```

### `StatefulParser`

When you need:
* expression optimization
* validation before evaluation,
use a stateful parser variant. A stateful parser derives from `StatefulParserBase` (which itself supplies all core functionality plus mutable `Expression` / `Variables`).

StatefulParsers are Transient by default contrary to the common Parsers, which are Singletons and do not keep state.
Because they are stateful, you can keep the expression (via the `Expression` property) and change the `Variables` before re-evaluation.

The StatefulParser also has a `Validate` method, which can be used to check if the expression is valid without actually evaluating it. This can be useful in interactive applications.
The method returns a collection of all the validation errors found.

Minimal example (mirrors the logic of the stateless `ItemParser`):

```cs
public class ItemStatefulParser(
    ILogger<ItemStatefulParser> logger,
    IOptions<TokenizerOptions> options,
    string expression,
    Dictionary<string, object?>? variables = null) : StatefulParser(logger, options, expression, variables)
{

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
        string actualFunctionName = _options.CaseSensitive ? functionName : functionName.ToLower();
        
        return actualFunctionName switch
        {
            "add" => (Item)args[0]! + (int)args[1]!,
            _ => base.EvaluateFunction(functionName, args)
        };
    }
}
```

### Initializing a stateful parser

There are many ways to create a statefule parser in a similar manner to the (stateless) `Parser` variants.

```cs
//v1 (implicitly create host too) 
var parser =  ParserApp.GetStatefulParser<ItemStatefulParser>();

//v2 (explicitly create host)
var parser = ParserApp.GetStatefulParserApp<ItemStatefulParser>().GetStatefulParser();

//v3 (explicitly add to existing host) 
var host  = Host.CreateDefaultBuilder()
.ConfigureServices((context, services) =>
    {
        services.AddStatefulParser<ItemStatefulParser>(context); 
        ...
    }).Build();
var parser = host.GetStatefulParser();

//v4 (keyed version, if multiple parsers are used)
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddStatefulParser<ItemStatefulParser>("item", TokenizerOptions.Default);
    }).Build();
var parser = host.GetStatefulParser("item");

```
We can then evaluate the expression the same way.

```cs
Item result = (Item)parser.Evaluate("a + add(b,4) + 5",
    new() {
        {"a", new Item { Name="foo", Value = 3}  },
        {"b", new Item { Name="bar"}  }
    });
Console.WriteLine(result); // foo bar 12
```

# Expression Tree

The library can build an expression tree for any parsed expression.  
APIs involved (namespace `ParserLibrary.ExpressionTree`):
- `TokenTree` (specialized expression tree for `Token`)
- `Tree<T>` (generic base)
- `NodeBase` (base class of the internal node type used by the tree)
- Printing via `Tree.Print(PrintType)` and string builders on `NodeBase` (`ToVerticalTreeString`, `ToHorizontalTreeString`, etc.)
- Optimizers: `OptimizeForDataTypes(...)`, `OptimizeForDataTypesUsingParser(...)`
- Traversal helpers via `TokenTree.GetPostfixTokens()` and `TokenTree.GetInfixTokens()`
- Cloning via `tree.DeepClone()` (returns a `TokenTree` at runtime)

Basic usage:

```cs
using ParserLibrary;
using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.ExpressionTree;
using ParserLibrary.Tokenizers; // Token
using System;

// Acquire a parser (double arithmetic)
var parser = ParserApp.GetDefaultParser();

// Build the expression tree (variables only needed for evaluation, not structure)
var tree = parser.GetExpressionTree("3 + 5 * (2 - 8) / a");

// Vertical print (recommended)
tree.Print2();                 // or: tree.Root.PrintVerticalTree(leftOffset: 0, gap: 0)

// Horizontal print (legacy styles)
tree.Root.PrintWithDashes();
// tree.Root.PrintWithSlashes();

// Metrics
int height  = tree.GetHeight();
int count   = tree.Count;
int leaves  = tree.GetLeafNodesCount();

// Traversals
List<Token> postfix = tree.GetPostfixTokens();  // Reverse Polish
List<Token> infix   = tree.GetInfixTokens();    // In-order tokens

// Clone + optimize (commutative (+, *) regrouping heuristic)
var optimizer  = new TreeOptimizer<Token>();
var optimized  = optimizer.OptimizeForDataTypes(tree, new Dictionary<string, Type> { { "a", typeof(int) } });

// Show both
Console.WriteLine("Original:");
tree.Print2();
Console.WriteLine("Optimized:");
optimized.Print2();
