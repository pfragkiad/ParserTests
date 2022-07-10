using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParserLibrary;

//TODO: Postfix unary tests.
//TODO: Add unary matches, when they are different from binary operators.

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
        this.TokenType = tokenType;
        Match = match;
    }

    //public string TokenType { get; set; }
    
    public TokenType TokenType { get; set;}
    public Match Match { get; set; }

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

