# ParserLibrary
_No Other Expression Parser, Ever_

### How it began
I wanted to write my "custom terminal" that used interactive commands with expressions. Other expression builders used only "numbers" as basic entities which I did not want; this is something too common. I wanted some variables to represent "musical notes" or "musical chords", or even "vectors" and "matrices" and some other to represent numbers.
The only way, was to build an Expression builder that could allow custom types. Obviously, the default capability of handling numerical values was needed as a start. Let's speed up with the some examples.

The library is based on dependency injection concepts and can be highly customized. 
There are 2 main classes: the ```Tokenizer``` and the ```Parser```. Both of them are base classes and adapt to the corresponding interfaces ```ITokenizer``` and ```IParser```.


## Examples

### Using the DefaultParser
```C#
//This is a simple expression, which uses variables and literals of type double, and the DefaultParser.
double result = (double)App.Evaluate( "-5.0+2*a", new() { { "a", 5.0 } });
Console.WriteLine(result);  //5

//2 variables example (spaces are obviously ignored)
double result2 = (double)App.Evaluate("-a + 500 * b + 2^3", new() { { "a", 5 }, { "b", 1 } });
Console.WriteLine(result2); //503
```
The first example is the same with the example below: the second way uses explicitly the ```DefaultParser```, which can be later overriden in order to use a custom Parser.

```C#
//The example below uses explicitly the DefaultParser.
var app = App.GetParserApp<DefaultParser>();
var parser = app.Services.GetParser();
double result = (double)parser.Evaluate("-5.0+2*a", new() { { "a", 5.0 } });
```

Let's use some functions already defined in the `DefaultParser`

```C#
double result3 = (double)App.Evaluate("cosd(phi)^2+sind(phi)^2", new() { { "phi", 45 } });
Console.WriteLine(result3); //  1.0000000000000002
```

### Adding new functions to the `DefaultParser`

That was the boring stuff, let's start adding some custom functionality. Let's add a custom function ```add3``` that takes 3 arguments. For this purpose, we create a new subclass of ```DefaultParser```. Note that we can add custom logging via dependency injection (some more examples will follow on this). For the moment, ignore the constructor. We assume that the ```add3``` functions sums its 3 arguments with a specific weight.

```C#
private class SimpleFunctionParser : DefaultParser
{
    public SimpleFunctionParser(ILogger<Parser> logger, ITokenizer tokenizer, IOptions<TokenizerOptions> options) : base(logger, tokenizer, options)
    {
    }

    protected override object EvaluateFunction(Node<Token> functionNode, Dictionary<Node<Token>, object> nodeValueDictionary)
    {
        double[] a = GetDoubleFunctionArguments(functionNode, nodeValueDictionary);

        switch (functionNode.Text.ToLower())
        {
            case "add3":
                    return a[0] + 2 * a[1] + 3 * a[2];
            default:
                return base.EvaluateFunction(functionNode, nodeValueDictionary);
        }
    }
}
```





