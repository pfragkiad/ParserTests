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
        if (Root is null) { NodeDictionary = []; ParentMap = null; return; }

        var dict = new Dictionary<T, Node<T>>();
        foreach (var n in Root.PostOrderNodes().Cast<Node<T>>())
        {
            if (n.Value is T v) dict[v] = n;
        }
        NodeDictionary = dict;

        // Keep parent map in sync when it was previously built.
        if (ParentMap is not null) BuildParentMap();
    }
    #endregion

    #region Parent map
    /// <summary>
    /// Maps each node to its parent node (<c>null</c> for the root).
    /// Populated on demand by <see cref="BuildParentMap"/> and kept in sync by
    /// <see cref="RebuildNodeDictionaryFromStructure"/>.
    /// </summary>
    public Dictionary<Node<T>, Node<T>?>? ParentMap { get; private set; }

    /// <summary>
    /// Builds (or rebuilds) the <see cref="ParentMap"/> from the current tree structure.
    /// O(n) traversal; call once after tree construction and after structural changes.
    /// </summary>
    public Dictionary<Node<T>, Node<T>?> BuildParentMap()
    {
        var map = new Dictionary<Node<T>, Node<T>?>();
        if (Root is not null)
            BuildParentMapRecursive(Root, null, map);
        ParentMap = map;
        return map;
    }

    private static void BuildParentMapRecursive(
        Node<T> node,
        Node<T>? parent,
        Dictionary<Node<T>, Node<T>?> map)
    {
        map[node] = parent;

        if (node.Left is Node<T> l)
            BuildParentMapRecursive(l, node, map);

        if (node.Right is Node<T> r)
            BuildParentMapRecursive(r, node, map);

        if (node.Other is not null)
            foreach (var child in node.Other.OfType<Node<T>>())
                BuildParentMapRecursive(child, node, map);
    }
    #endregion


    #region Navigation
    /// <summary>
    /// Returns all ancestor nodes of the specified node (excluding the node itself).
    /// Requires <see cref="ParentMap"/> to be built via <see cref="BuildParentMap"/> first.
    /// </summary>
    /// <param name="node">Target node.</param>
    /// <param name="rootFirst">If true the list is ordered root->parent; otherwise parent->root.</param>
    public List<Node<T>> GetAncestors(Node<T> node, bool rootFirst = false)
    {
        var ancestors = new List<Node<T>>();
        if (Root is null || node is null || ParentMap is null) return ancestors;

        var current = node;
        while (ParentMap.TryGetValue(current, out var parent) && parent is not null)
        {
            ancestors.Add(parent);
            current = parent;
        }

        // ancestors is built parent->root; reverse if rootFirst is requested
        if (rootFirst) ancestors.Reverse();
        return ancestors;
    }
    #endregion
}

