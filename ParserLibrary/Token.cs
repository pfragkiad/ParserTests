using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParserLibrary;

public class Token : IComparable<Token>
{
    public Token(string tokenType, Match match)
    {
        this.TokenType = tokenType;
        Match = match;
    }

    public string TokenType { get; set; }
    public Match Match { get; set; }


    public const string LiteralTokenType = "literal";
    public const string IdentifierTokenType = "identifier";
    public const string OperatorTokenType = "operator";
    public const string OperatorUnaryTokenType = "operator unary";

    public const string OpenParenthesisTokenType = "open parenthesis";
    public const string FunctionOpenParenthesisTokenType = "function open parenthesis";
    public const string CloseParenthesisTokenType = "closed parenthesis";
    public const string ArgumentSeparatorTokenType = "argument separator";

    public string Text => Match.Value;

    public int Index => Match.Index;

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

