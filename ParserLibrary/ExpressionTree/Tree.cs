using ParserLibrary.Tokenizers;

namespace ParserLibrary.ExpressionTree;

public class Tree<T>
{
    public required Node<T> Root { get; set; }
    public Dictionary<Token, Node<T>> NodeDictionary { get; internal set; } = [];
    public int GetHeight() => (Root?.GetHeight() - 1) ?? 0;

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

    #region Get tokens from the tree
    public List<Token> GetPostfixTokens()
    {
        if (Root is null) return [];
        var list = new List<Token>();
        foreach (Node<Token> n in Root.PostOrderNodes().Cast<Node<Token>>())
            list.Add(n.Value ?? Token.Null);
        return list;
    }

    public List<Token> GetInfixTokens()
    {
        if (Root is null) return [];
        var list = new List<Token>();
        foreach (Node<Token> n in Root.InOrderNodes().Cast<Node<Token>>())
            list.Add(n.Value ?? Token.Null);
        return list;
    }

    public string GetExpressionString(TokenizerOptions options, bool spacesAroundOperators = true) =>
        GetExpressionString(options.TokenPatterns, spacesAroundOperators);

    public string GetExpressionString(TokenPatterns patterns, bool spacesAroundOperators = true)
    {
        if (typeof(T) != typeof(Token) || Root.Value is not Token)
            return Root.ToParenthesizedString();

        var fmtOptions = new ExpressionFormatterOptions(SpacesAroundBinaryOperators: spacesAroundOperators);
        return ExpressionFormatter.Format((Tree<Token>)(object)this, patterns, fmtOptions);
    }
    #endregion

    #region Cloning
    public Tree<T> DeepClone()
    {
        if (Root is null)
            throw new InvalidOperationException("Cannot clone a tree with a null root.");

        var cloneMap = new Dictionary<Node<T>, Node<T>>();
        var clonedRoot = Root.DeepClone(cloneMap);
        var clonedDict = new Dictionary<Token, Node<T>>();

        foreach (var kvp in NodeDictionary)
        {
            var clonedToken = kvp.Key.Clone();
            if (cloneMap.TryGetValue(kvp.Value, out var clonedNode))
                clonedDict[clonedToken] = clonedNode;
        }

        return new Tree<T>
        {
            Root = clonedRoot,
            NodeDictionary = clonedDict
        };
    }
    #endregion
}

