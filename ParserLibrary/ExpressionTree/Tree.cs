namespace ParserLibrary.ExpressionTree;

public class Tree<T> where T : notnull
{
    public required Node<T> Root { get; set; }
    public Dictionary<T, Node<T>> NodeDictionary { get; internal set; } = [];
    public int GetHeight() => (Root?.GetHeight() - 1) ?? 0;

    public List<T> GetPostfixValues(T defaultValue)
    {
        if (Root is null) return [];
        var list = new List<T>();
        foreach (Node<T> n in Root.PostOrderNodes().Cast<Node<T>>())
            list.Add(n.Value ?? defaultValue);
        return list;
    }

    public List<T> GetInfixValues(T defaultValue)
    {
        if (Root is null) return [];
        var list = new List<T>();
        foreach (Node<T> n in Root.InOrderNodes().Cast<Node<T>>())
            list.Add(n.Value ?? defaultValue);
        return list;
    }

    #region Printing
    public void Print(PrintType printType = PrintType.Vertical) =>
        Console.WriteLine(ToString(printType));

    public override string ToString() => Root.ToParenthesizedString();
    public string ToString(PrintType type) => Root.ToString(type);
    #endregion

    public int GetLeafNodesCount() =>
        NodeDictionary.Count(e => e.Value.Left is null && e.Value.Right is null);
    public int Count => NodeDictionary.Count;

    public static int MinimumNodesCount(int height) => height + 1;
    public static int MaximumNodesCount(int height) => (1 << height) - 1;

    #region Cloning
    // Factory to preserve the runtime type during cloning
    protected virtual Tree<T> CreateInstance(Node<T> Root, Dictionary<T, Node<T>> NodeDictionary) => new()
    {
        Root = Root,
        NodeDictionary = NodeDictionary
    };


    protected virtual Tree<T> CreateInstance(Tree<T> source) => new()
    {
        Root = source.Root,
        NodeDictionary = source.NodeDictionary
    };

    public Tree<T> DeepClone()
    {
        if (Root is null)
            throw new InvalidOperationException("Cannot clone a tree with a null root.");

        var cloneMap = new Dictionary<Node<T>, Node<T>>();
        var clonedRoot = Root.DeepClone(cloneMap);

        var clonedDict = new Dictionary<T, Node<T>>(NodeDictionary.Count);
        foreach (var kvp in NodeDictionary)
        {
            if (!cloneMap.TryGetValue(kvp.Value, out var clonedNode))
                continue;

            var clonedKey = clonedNode.Value ?? kvp.Key; // safer than arbitrary default
            clonedDict[clonedKey] = clonedNode;
        }

        var clone = CreateInstance( clonedRoot, clonedDict);
        return clone;
    }
    #endregion

    #region Maintenance
    // Rebuilds NodeDictionary to match the current tree structure.
    public void RebuildNodeDictionaryFromStructure()
    {
        if (Root is null) { NodeDictionary = []; return; }

        var dict = new Dictionary<T, Node<T>>();
        foreach (var n in Root.PostOrderNodes().Cast<Node<T>>())
        {
            if (n.Value is T v) dict[v] = n;
        }
        NodeDictionary = dict;
    }
    #endregion
}

