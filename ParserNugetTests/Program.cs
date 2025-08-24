using Microsoft.Extensions.DependencyInjection;
using ParserLibrary;
using ParserLibrary.Parsers.Common;
using System.Numerics;


//https://www.nuget.org/packages/ParserLibrary/1.0.0#readme-body-tab
//var parser = App.GetParserApp<DefaultParser>("appsettings.json").Services.GetService<IParser>();
//var tree = parser.GetExpressionTree("-j+8.0-(+f(x,y*200/21-dad^8))");
//tree.Print();

//var parser = App.Evaluate("422+(5*sind(90)");

//dotnet add package ParserLibrary --version 1.0.0


//string s = "-5.0+2*a";
//double result = (double)App.Evaluate(s, new() { { "a", 5.0 } });
//Console.WriteLine(result);


//double result2 = (double)App.Evaluate("-a + 500 * b + 2^3", new() { { "a", 5 }, { "b", 1 } });
//Console.WriteLine(result2);


//double result3 = (double)App.Evaluate("cosd(phi)^2+sind(phi)^2", new() { { "phi", 45 } });
//Console.WriteLine(result3);

//var tree = App.GetDefaultParser().GetExpressionTree("f(a1,a2,a3,a4)");
//tree.Print();


Complex c1 = new(1, 1);
Console.WriteLine(Complex.Cos(c1));

var cparser = App.GetComplexParser();
Console.WriteLine(cparser.Evaluate("round(cos((1+i)/(8+i)),4)"));


Console.WriteLine(cparser.Evaluate("round(exp(i*pi),8)")); //(-1, 0)  (Euler is correct!)


var vparser = App.GetVector3Parser();

Vector3 v1 = new(1, 4, 2), v2 = new(2, -2, 0);

Console.WriteLine(vparser.Evaluate("!(v1+3*v2)", //! means normalize vector
   new() { { "v1", v1 }, { "v2", v2 } })); //<0,92717266. -0,26490647. 0,26490647>

Console.WriteLine(vparser.Evaluate("10 + 3 * v1^v2", // ^ means cross product
   new() { { "v1", v1 }, { "v2", v2 } })); //<22. 22. -20>


Console.WriteLine(vparser.Evaluate("v1@v2", // @ means dot product
   new() { { "v1", v1 }, { "v2", v2 } })); //-6

Console.WriteLine(vparser.Evaluate("lerp(v1, v2, 0.5)", // lerp (linear combination of vectors)
   new() { { "v1", v1 }, { "v2", v2 } })); //<1,5. 1. 1>

Console.WriteLine(vparser.Evaluate("6*ux -12*uy + 14*uz")); //<6. -12. 14>


var tree = App
    .GetDefaultParser()
    .GetExpressionTree("cos(sin(a+b, 3+4) +1,+2, set(5,6,7+8,9)+1, sda(sin(2+2,2),sin(s,s,s)) , sin(s,s,s,s)*sin(asd,212,2123,212))");

tree.Print();

//get the cos node
var cosNode = tree.NodeDictionary.Where(e => e.Key.Text == "cos").FirstOrDefault().Value;
Console.WriteLine(cosNode.GetFunctionArgumentsCount(","));






