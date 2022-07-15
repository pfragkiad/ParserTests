using Microsoft.Extensions.DependencyInjection;
using ParserLibrary;


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


