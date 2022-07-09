using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.ExpressionTree;

public abstract class NodeBase
{
    public string Text { get; set; }

    public NodeBase Left { get; set; } //1

    public NodeBase Right { get; set; } //0

    public List<NodeBase>? Other { get; set; } //>=2 (added for functions with more than 2 arguments)


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
        int maxOtherHeight = Other?.Select(n => n.GetHeight()).Max() ?? 0;

        return Math.Max(maxOtherHeight,Math.Max(leftHeight,rightHeight)) + 1;
    }

    //https://www.csharpstar.com/csharp-program-to-implement-binary-search-tree-traversal/#:~:text=There%20are%20three%20traversal%20methods,of%20the%20node%20key%20values.&text=%E2%80%93%20A%20postorder%20traversal%2C%20the%20method,then%20over%20the%20right%20subtrees.
    public IEnumerable<NodeBase> PreOrderNodes()
    {
        yield return this;

        if (Other?.Any() ?? false) //PRE ORDER TESTED!
            foreach (var node in (Other as IEnumerable<NodeBase>).Reverse())
                yield return node;

        if (Left is not null)
            foreach (var node in Left.PreOrderNodes())
                yield return node;

        if (Right is not null)
            foreach (var node in Right.PreOrderNodes())
                yield return node;
    }

    public IEnumerable<NodeBase> PostOrderNodes() 
    {
        if(Other?.Any() ?? false) //POST ORDER TESTED!
            foreach(var node in (Other as IEnumerable<NodeBase>).Reverse())
                yield return node;

        if (Left is not null)
            foreach (var node in Left.PostOrderNodes())
                yield return node;

        if (Right is not null)
            foreach (var node in Right.PostOrderNodes())
                yield return node;

        yield return this;
    }

    public IEnumerable<NodeBase> InOrderNodes() //WORKS ONLY
    {
        if (Other?.Any() ?? false) //Argument case only (Left cannot precede function in any case)
        {
            //to be consistent and remove this case, all Other should be stored on the Right Node
            yield return this;

            foreach (var node in (Other as IEnumerable<NodeBase>).Reverse())
                yield return node;

            if (Left is not null)
                foreach (var node in Left.InOrderNodes())
                    yield return node;

            if (Right is not null)
                foreach (var node in Right.InOrderNodes())
                    yield return node;
        }
        else
        {
            if (Left is not null)
                foreach (var node in Left.InOrderNodes())
                    yield return node;

            yield return this;

            if (Right is not null)
                foreach (var node in Right.InOrderNodes())
                    yield return node;
        }
    }


}
