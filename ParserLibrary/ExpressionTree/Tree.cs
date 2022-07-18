using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public class Tree<T>
{
    public Node<T> Root { get; set; }

    public Dictionary<Token, Node<T>> NodeDictionary { get; internal set; }

    public int GetHeight() => (Root?.GetHeight() - 1) ?? 0;

    public int Count { get => NodeDictionary.Count; }

    public void Print(int topMargin = 2, int leftMargin = 2, bool withSlashes = false)
    {
        if (withSlashes)
            Root.PrintWithSlashes(topMargin: topMargin, leftMargin: leftMargin);
        else Root.PrintWithDashes(topMargin: topMargin, leftMargin: leftMargin);
    }

    public int CountLeafNodes
    {
        get => NodeDictionary.
            Count(e => e.Value.Left is null && e.Value.Right is null);
    }

    public static int MinimumNodesCount(int height) => height + 1;
    public static int MaximumNodesCount(int height) => (1 << height) - 1; //2^h-1
}

