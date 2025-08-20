using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public class Tree<T>
{
    public required Node<T> Root { get; set; }

    public Dictionary<Token, Node<T>> NodeDictionary { get; internal set; } = [];

    public int GetHeight() => (Root?.GetHeight() - 1) ?? 0;

    public int Count { get => NodeDictionary.Count; }

    public void Print(int topMargin = 2, int leftMargin = 2, bool withSlashes = false)
    {
        if (withSlashes)
            Root.PrintWithSlashes(topMargin: topMargin, leftMargin: leftMargin);
        else Root.PrintWithDashes(topMargin: topMargin, leftMargin: leftMargin);
    }

    public int GetLeafNodesCount()
    {
        return NodeDictionary.
             Count(e => e.Value.Left is null && e.Value.Right is null);
    }

    public static int MinimumNodesCount(int height) => height + 1;
    public static int MaximumNodesCount(int height) => (1 << height) - 1; //2^h-1

    #region Get tokens from the tree

    /// <summary>
    /// Gets the tokens in postfix order from the expression tree.
    /// </summary>
    /// <returns>A list of tokens in postfix (reverse Polish) notation order</returns>
    public List<Token> GetPostfixTokens()
    {
        if (Root is null)
            return [];

        var postfixTokens = new List<Token>();

        // Post-order traversal gives us postfix notation
        var nodes = Root.PostOrderNodes().Cast<Node<Token>>();

        foreach(Node<Token> node in nodes)
            postfixTokens.Add(node.Value ?? Token.Null);

        return postfixTokens;
    }

    /// <summary>
    /// Gets the tokens in infix order from the expression tree.
    /// </summary>
    /// <returns>A list of tokens in infix notation order</returns>
    public List<Token> GetInfixTokens()
    {
        if (Root is null)
            return [];

        var infixTokens = new List<Token>();

        var nodes = Root.InOrderNodes().Cast<Node<Token>>();

        foreach (Node<Token> node in nodes)
            infixTokens.Add(node.Value ?? Token.Null);

        return infixTokens;

    }

    #endregion

    #region Cloning

    public Tree<T> DeepClone()
    {
        if (Root is null)
            throw new InvalidOperationException("Cannot clone a tree with a null root.");

        // Use the shared clone map for the entire tree
        var cloneMap = new Dictionary<Node<T>, Node<T>>();

        // Clone the root and all descendants
        var clonedRoot = Root.DeepClone(cloneMap);

        // Reconstruct the NodeDictionary for the cloned tree
        var clonedNodeDictionary = new Dictionary<Token, Node<T>>();

        foreach (var kvp in NodeDictionary)
        {
            var originalToken = kvp.Key;
            var originalNode = kvp.Value;

            // Clone the token
            var clonedToken = originalToken.Clone();

            // Get the corresponding cloned node
            if (cloneMap.TryGetValue(originalNode, out var clonedNode))
            {
                clonedNodeDictionary[clonedToken] = clonedNode;
            }
        }

        return new Tree<T>
        {
            Root = clonedRoot,
            NodeDictionary = clonedNodeDictionary
        };
    }



    #endregion
}

