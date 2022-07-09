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
    public const string FunctionOpenParenthesisTokenType = "function open parenthesis";
    public const string CloseParenthesisTokenType = "closed parenthesis";
    public const string ArgumentSeparatorTokenType = "argument separator";

    public string Value => Match.Value;

    public int Index => Match.Index;

    public override string ToString() => Match.Value;

    public int CompareTo(Token? other)
    {
        if (other is null) return 1;

        return
            Match.Index != other.Match.Index ?
            Match.Index - other.Match.Index :
            //we force string comparison if they have the same index
            Match.Value.CompareTo(other.Match.Value);;
    }
}

