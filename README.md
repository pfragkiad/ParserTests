# ParserLibrary
## _No Other Expression Parser, Ever_

### How it began
I wanted to write my "custom terminal" that used interactive commands with expressions. Other expression builders used only "numbers" as basic entities which I did not want; this is something too common. I wanted some variables to represent "musical notes" or "musical chords", or even "vectors" and "matrices" and some other to represent numbers.
The only way, was to build an Expression builder that could allow custom types. Obviously, the default capability of handling numerical values was needed as a start. Let's speed up with the some examples.

The library is based on dependency injection concepts and can be highly customized. 
There are 2 main classes: the ```Tokenizer``` and the ```Parser```. Both of them are base classes and adapt to the corresponding interfaces ```ITokenizer``` and ```IParser```.


## Examples
```C#
//This is a simple expression, which uses variables of type double, and the DefaultParser.
string s= "-5.0+2*a";
double result = (double)App.Evaluate(s, new Dictionary<string, object>{{ "a", 5.0}}); 
```
The above is the same with the example below: the second way uses explicitly the ```DefaultParser```, which later can be overriden in other examples.

```C#
//This is a simple expression, which uses variables of type double, and the DefaultParser.
string s= "-5.0+2*a";
var app = App.GetParserApp<DefaultParser>();
var parser = app.Services.GetParser();
double result = (double)parser.Evaluate(s, new Dictionary<string, object>{{ "a", 5.0}}); 
```
