﻿// See https://aka.ms/new-console-template for more information
using ParserLibrary;

using ParserLibrary.ExpressionTree;
using System.Diagnostics;



//string expr = "asdf+(2-5*a)* d1-3^2";

//https://www.youtube.com/watch?v=PAceaOSnxQs
//string expr = "K+L-M*N+(O^P)*W/U/V*T+Q";
//string expr = "a*b/c+e/f*g+k-x*y";
//string expr = "2^3^4";
//string expr  = "a+tan(bg)";
//string expr = "a+tan(bg,ab)";
//string expr = "a+tan(a1,a2,a3,a4)"; 
string expr = "a+tan(8+5) + sin(321+asd*2^2)";
//string expr = "a+sin(1)*tan(8+5,a+2^2*(34-h),98)";
//string expr = "0.1*sin(a1,a2)+90";

//TODO: Add support for unary operators
//TODO: Add support for different literal data types


var parserApp = App.GetParserApp();
var parser = parserApp.Services.GetParser();

//var tree = parser.Parse(expr);
//tree.Root.PrintWithDashes();
//Console.WriteLine($"Tree nodes: {tree.Count}");
//Console.WriteLine($"Tree leaf nodes: {tree.CountLeafNodes}");
//Console.WriteLine($"Tree height: {tree.GetHeight()}");
//Console.WriteLine("Post order traversal: " + string.Join(" ", tree.Root.PostOrderNodes().Select(n => n.Text)));
//Console.WriteLine("Pre order traversal: " + string.Join(" ", tree.Root.PreOrderNodes().Select(n => n.Text)));
//Console.WriteLine("In order traversal: " + string.Join(" ", tree.Root.InOrderNodes().Select(n => n.Text)));

//expr = "a+tan(8+5) + sin(321+asd*2^2)"; //returns 860
expr = "a+tan(8+5) * sin(321,asd)"; //returns 43038

parser.Parse(expr).Root.PrintWithDashes(0,0);