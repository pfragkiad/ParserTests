using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLibrary.Definitions;

public class LambdaExpression
{
    public string[] ParamList { get; init; } = [];
    public required string Body { get; init; }

    public static LambdaExpression Create(string[] parameters, string body) =>
        new() { ParamList = parameters, Body = body };

}


