using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParserTests;

public record Token(
    string TokenType,
    Match Match) : IComparable<Token>

{
    public const string LiteralTokenType = "literal";
    public const string IdentifierTokenType = "identifier";
    public const string OperatorTokenType = "operator";
    public const string OpenParenthesisTokenType = "open parenthesis";
    public const string CloseParenthesisTokenType = "closed parenthesis";

    public string Value => Match.Value;

    public override string ToString() => Match.Value;

    public int CompareTo(Token? other)
    {
        if (other is null) return 1;

        return Match.Index - other.Match.Index;
    }
}

