namespace ParserLibrary.Tokenizers;
using System.Text.RegularExpressions;

public class Token : IComparable<Token>
{
    // ── span-based storage (no allocation until Text is requested) ──────────
    private ReadOnlyMemory<char> _memory;   // slice of the original input
    private int _length;                    // length within _memory starting at _index

    // ── legacy eager-string storage (used when created from string ctor) ────
    private string? _text;                  // lazily materialized or eagerly set

    protected char _singleChar = '\0';
    protected int _index = -1;

    // ── factory: zero-allocation path from regex Match ──────────────────────
    /// <summary>Creates a token backed by a memory slice — no string allocated.</summary>
    public static Token FromMatch(Match match, ReadOnlyMemory<char> inputMemory,
        TokenType tokenType, string? captureGroup = null) =>
        new(tokenType, inputMemory, match.Index, match.Length, captureGroup);

    /// <summary>Legacy overload that still works (allocates from Match.Value).</summary>
    public static Token FromMatch(Match match, TokenType tokenType, string? captureGroup = null) =>
        new(tokenType, match.Value, match.Index, captureGroup);

    // ── span-based ctor (preferred, zero-allocation) ─────────────────────────
    public Token(TokenType tokenType, ReadOnlyMemory<char> inputMemory, int index, int length,
        string? captureGroup = null)
    {
        TokenType = tokenType;
        _memory   = inputMemory;
        _index    = index;
        _length   = length;
        CaptureGroup = captureGroup;
    }

    // ── legacy string ctor (backward-compatible) ──────────────────────────────
    public Token(TokenType tokenType, string text, int index, string? captureGroup = null)
    {
        TokenType    = tokenType;
        _text        = text;
        _index       = index;
        _length      = text.Length;
        _memory      = ReadOnlyMemory<char>.Empty; // Text already stored; no memory slice needed
        CaptureGroup = captureGroup;
    }

    // ── single-char ctor (parentheses / separators — already cheap) ──────────
    public Token(TokenType tokenType, char singleChar, int index)
    {
        TokenType  = tokenType;
        _singleChar = singleChar;
        _text      = _singleChar.ToString();
        _index     = index;
        _length    = 1;
        _memory    = ReadOnlyMemory<char>.Empty;
    }

    //We allow TokenType to be changed later.
    public TokenType TokenType { get; set; }

    // NEW: name of the first successful named capture group (identifier/literal subtype)
    // Null if pattern has no named groups or none matched.
    public string? CaptureGroup { get; set; }

    // ── zero-allocation span accessor ────────────────────────────────────────
    /// <summary>
    /// Returns the token text as a <see cref="ReadOnlySpan{T}"/> without allocating a string.
    /// Use this in hot paths; use <see cref="Text"/> only when a managed string is required.
    /// </summary>
    public ReadOnlySpan<char> Span =>
        _memory.IsEmpty
            ? (_text is not null ? _text.AsSpan() : ReadOnlySpan<char>.Empty)
            : _memory.Span.Slice(_index, _length);

    // ── Text: lazy materialization ────────────────────────────────────────────
    public string Text
    {
        get => _text ??= new string(Span);
        set
        {
            _text   = value;
            _memory = ReadOnlyMemory<char>.Empty;  // invalidate memory slice
            _length = value?.Length ?? 0;
        }
    }

    public char SingleChar
    {
        get => _singleChar;
        set => _singleChar = value;
    }

    public int Index
    {
        get => _index;
        set => _index = value;
    }

    public static Token Null => new(TokenType.Literal, "", -1);

    public bool IsNull => _index == -1;

    public override string ToString() => Text;

    public int CompareTo(Token? other)
    {
        if (other is null) return 1;
        if (_index != other._index) return _index - other._index;
        if (_index == -1) return 0; // not found
        // force string comparison when indices are equal
        return Text.CompareTo(other.Text);
    }

    /// <summary>Creates a deep clone of this token (shares memory slice, no extra allocation).</summary>
    public Token Clone()
    {
        // single-char token
        if (_singleChar != '\0' && (_text is null || (_text.Length == 1 && _text[0] == _singleChar)))
            return new Token(TokenType, _singleChar, _index) { CaptureGroup = CaptureGroup };

        // span-based token (share memory slice)
        if (!_memory.IsEmpty)
            return new Token(TokenType, _memory, _index, _length, CaptureGroup)
            {
                SingleChar = _singleChar
            };

        // legacy string token
        return new Token(TokenType, _text ?? "", _index, CaptureGroup)
        {
            SingleChar = _singleChar
        };
    }
}

