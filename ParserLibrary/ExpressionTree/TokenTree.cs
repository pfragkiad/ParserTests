namespace ParserLibrary.ExpressionTree;

/// <summary>
/// Extension methods that apply only when the tree holds Token nodes (Tree&lt;Token&gt;).
/// Moved out of the generic Tree&lt;T&gt; to isolate Token-specific concerns.
/// </summary>
public partial class TokenTree : Tree<Token>
{
    #region Token sequence extraction
    public List<Token> GetPostfixTokens() =>
        GetPostfixValues(Token.Null);

    public List<Token> GetInfixTokens() =>
        GetInfixValues(Token.Null);
    #endregion

    // Ensure base DeepClone() returns a TokenTree at runtime
    protected override Tree<Token> CreateInstance(Node<Token> root, Dictionary<Token, Node<Token>> nodeDictionary) => new TokenTree
    {
        Root = root,
        NodeDictionary = nodeDictionary
    };

    protected override Tree<Token> CreateInstance(Tree<Token> source) => new TokenTree
    {
        Root = source.Root,
        NodeDictionary = source.NodeDictionary
    };

    public TokenTree DeepCloneTyped() => (TokenTree)CreateInstance(DeepClone());

    #region Expression reconstruction
    public string GetExpressionString(TokenizerOptions options, bool spacesAroundOperators = true) =>
        GetExpressionString(options.TokenPatterns, spacesAroundOperators);

    public string GetExpressionString(TokenPatterns patterns, bool spacesAroundOperators = true)
    {
       // return Root.ToParenthesizedString();

        // Defensive: if root token missing just fall back to parenthesized textual form.
        if (Root.Value is not Token)
            return Root.ToParenthesizedString();

        var fmtOptions = new ExpressionFormatterOptions(SpacesAroundBinaryOperators: spacesAroundOperators, SpaceAfterSeparator: spacesAroundOperators);
        return ExpressionFormatter.Format(this, patterns, fmtOptions);
    }
    #endregion

    #region Factory helpers
    public static TokenTree From(Tree<Token> tree) =>

        tree is TokenTree tt ? tt : new TokenTree()
        {
            Root = tree.Root,
            NodeDictionary = tree.NodeDictionary
        };
    #endregion


    #region Get node helpers


    public IEnumerable<Node<Token>> GetFunctionNodes() =>
        base.NodeDictionary.Values.Where(n =>
        {
            if (n.Value is null) return false;
            Token t = n.Value;
            if (t.IsNull) return false;
            return t.TokenType == TokenType.Function;
        });

    #endregion



}

