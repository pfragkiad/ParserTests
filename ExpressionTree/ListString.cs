using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.ExpressionTree;

/// <summary>
/// This is an extension to the List, however it overrides the ToString() method. This is needed for the Node&lt;T> to allow a list to be passed using the desired ToString override.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ListToString<T> : List<T>
{
    public override string ToString()
    {
        return string.Join(" ", this.Select(v=>v.ToString()));
    }

    public string JoinSeparator { get; set; } = " ";

}
