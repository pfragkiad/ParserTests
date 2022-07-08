using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.Expressions;

public abstract class NodeBase
{
    public string Text { get; set; }

    public NodeBase Left { get; set; }
    
    public NodeBase Right { get; set; }


    public NodeBase(string text)
    {
        Text = text;
    }

    public override string ToString() => Text;
}
