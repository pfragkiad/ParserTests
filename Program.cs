// See https://aka.ms/new-console-template for more information
using ParserTests;

using ParserTests.ExpressionTree;



//string expr = "asdf+(2-5*a)* d1-3^2";

//https://www.youtube.com/watch?v=PAceaOSnxQs
string expr = "K+L-M*N+(O^P)*W/U/V*T+Q";
//string expr = "a*b/c+e/f*g+k-x*y";
//string expr = "2^3^4";

var parserApp = App.GetParserApp();
//var tokenizer = parserApp.Services.GetTokenizer()!;
//tokenizer.Tokenize(expr);
var parser = parserApp.Services.GetParser();
var tree = parser.Parse(expr);
tree.Root.PrintWithDashes();

Console.WriteLine($"Tree nodes: {tree.Count}");
Console.WriteLine($"Tree leaf nodes: {tree.CountLeafNodes}");

Console.WriteLine($"Tree height: {tree.GetHeight()}");