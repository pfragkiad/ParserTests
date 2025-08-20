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
        var nodeToTokenMap = NodeDictionary.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        // Post-order traversal gives us postfix notation
        foreach (var node in Root.PostOrderNodes().Cast<Node<T>>())
        {
            if (nodeToTokenMap.TryGetValue(node, out var token))
            {
                // Skip null tokens that were added during parsing for empty operands
                if (!token.IsNull)
                    postfixTokens.Add(token);
            }
        }

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
        var nodeToTokenMap = NodeDictionary.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        // In-order traversal gives us infix notation
        foreach (var node in Root.InOrderNodes().Cast<Node<T>>())
        {
            if (nodeToTokenMap.TryGetValue(node, out var token))
            {
                // Skip null tokens that were added during parsing for empty operands
                if (!token.IsNull)
                    infixTokens.Add(token);
            }
        }

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

