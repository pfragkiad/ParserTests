namespace ParserLibrary.Tokenizers;

public class TokenPatterns //NOT records here!
{
    public string Identifier { get; set; }

    public string Literal { get; set; }

    public string OpenParenthesis { get; set; } = "(";

    public string CloseParenthesis { get; set; } = ")";

    public string ArgumentSeparator { get; set; } = ",";

    public List<UnaryOperator>? Unary { get; set; }

    public List<Operator>? Operators { get; set; } = new List<Operator>();

    public Dictionary<string, Operator> OperatorDictionary { get => Operators.ToDictionary(op => op.Name, op => op); }

    public Dictionary<string, UnaryOperator> UnaryOperatorDictionary { get => Unary.ToDictionary(op => op.Name, op => op); }

}
