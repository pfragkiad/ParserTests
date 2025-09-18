namespace ParserLibrary.Tokenizers;
using System.Text.RegularExpressions;

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

    public static Token FromMatch(Match match, TokenType tokenType, string? captureGroup = null) =>
        new(tokenType, match.Value, match.Index, captureGroup);


    public Token(TokenType tokenType, string text, int index, string? captureGroup = null) //for fixed value tokens
    {
        TokenType = tokenType;
        _text = text;
        _index = index;
        if (captureGroup is not null)
            CaptureGroup = captureGroup;
    }

    public Token(TokenType tokenType, char singleChar, int index) //for fixed value tokens
    {
        TokenType = tokenType;
        _singleChar = singleChar;
        _text = _singleChar.ToString();
        _index = index;
    }

    //We allow TokenType to be changed later.
    public TokenType TokenType { get; set; }

    ////Used only when regex is used.

    //protected Match? _match;
    //public Match? Match
    //{
    //    get => _match;
    //    set
    //    {
    //        _match = value;
    //        if (_match is null) return;
    //        _text = value?.Value ?? "";
    //        _index = value?.Index ?? -1;
    //    }
    //}

    // NEW: name of the first successful named capture group (identifier/literal subtype)
    // Null if pattern has no named groups or none matched.
    public string? CaptureGroup { get; set; }


    protected string _text = "";

    public string Text
    {
        get
        {
            return _text;
        }
        set { _text = value; }
    }

    protected char _singleChar = '\0';
    public char SingleChar
    {
        get
        {
            return _singleChar;
        }
        set { _singleChar = value; }
    }



    protected int _index = -1;

    public int Index
    {
        get
        {
            return _index;
        }

        set { _index = value; }
    }

    public static Token Null => new(TokenType.Literal, "", -1);

    public bool IsNull => _index == -1;

    public override string ToString() => _text ?? "";

    public int CompareTo(Token? other)
    {
        if (other is null) return 1;

        if (_index != other._index) return
                _index - other._index;

        if (_index == -1) return 0; //not found 

        //we force string comparison if they have the same index
        return
            _text.CompareTo(other?._text); ;
    }

    /// <summary>
    /// Creates a deep clone of this token.
    /// </summary>
    /// <returns>A new Token instance with the same properties</returns>
    public Token Clone()
    {
        var cloned = new Token(TokenType, _text, _index, CaptureGroup)
        {
            SingleChar = _singleChar,
        };

        return cloned;
    }
}

