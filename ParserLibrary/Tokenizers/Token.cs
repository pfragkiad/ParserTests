namespace ParserLibrary.Tokenizers;

public enum TokenType
{
    Literal,
    Identifier,
    Operator,
    OperatorUnary,
    OpenParenthesis,
    Function,
    ClosedParenthesis,
    ArgumentSeparator
}

public class Token : IComparable<Token>
{
    public Token(TokenType tokenType, Match match)
    {
        TokenType = tokenType;
        Match = match;
    }

    //public string TokenType { get; set; }

    public TokenType TokenType { get; set; }

    //Used only when regex is used.
    public Match? Match { get; set; }


    protected string? _text;

    public string? Text
    {
        get
        {
            return Match is not null ? Match.Value :
                _text ?? string.Empty;
        }
        set { _text = value; }
    }


    protected int? _index = -1;

    public int Index
    {
        get
        {
            return Match is not null ? Match.Index :
                (_index ?? -1);
        }

        set { _index = value; }
    }

    public static Token Null => new(TokenType.Literal, Match.Empty);

    public bool IsNull => Match == Match.Empty;

    public override string ToString() => Match.Value;

    public int CompareTo(Token? other)
    {
        if (other is null) return 1;

        return
            Match.Index != other.Match.Index ?
            Match.Index - other.Match.Index :
            //we force string comparison if they have the same index
            Match.Value.CompareTo(other.Match.Value); ;
    }
}

