using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserTests.ExpressionTree;

public class Tree<T>
{
    public Node<T> Root { get; set; }

    public Dictionary<Token, Node<T>> NodeDictionary { get; internal set; }

    public int GetHeight() => (Root?.GetHeight() - 1) ?? 0;

    public int Count { get => NodeDictionary.Count; }

    public int CountLeafNodes
    {
        get => NodeDictionary.
            Count(e => e.Value.Left is null && e.Value.Right is null);
    }

    public static int MinimumNodesCount(int height) => height + 1;
    public static int MaximumNodesCount(int height) => (1 << height) - 1; //2^h-1
}

