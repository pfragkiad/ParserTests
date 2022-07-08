using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.ExpressionTree;

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

    public int GetHeight()
    {
        //https://www.baeldung.com/cs/binary-tree-height#:~:text=The%20height%20of%20a%20binary%20tree%20is%20the%20height%20of,the%20depth%20of%20the%20tree.
        int leftHeight = Left?.GetHeight() ?? 0;
        int rightHeight = Right?.GetHeight() ?? 0;
        return (leftHeight >= rightHeight ? leftHeight : rightHeight) + 1;
    }
}
