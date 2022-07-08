// See https://aka.ms/new-console-template for more information
using ParserTests;
using ParserTests.Expressions;



//string expr = "asdf+(2-5*a)* d1-3^2";

//https://www.youtube.com/watch?v=PAceaOSnxQs
string expr = "K+L-M*N+(O^P)*W/U/V*T+Q";
//string expr = "a*b/c+e/f*g+k-x*y";

var parserApp = App.GetParserApp();
//var tokenizer = parserApp.Services.GetTokenizer()!;
//tokenizer.Tokenize(expr);
var parser = parserApp.Services.GetParser();
var tree = parser.Parse(expr);
tree.Root.Print();