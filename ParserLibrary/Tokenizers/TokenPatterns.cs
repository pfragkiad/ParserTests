namespace ParserLibrary.Tokenizers;

public class TokenPatterns //NOT records here!
{
    public string? Identifier { get; set; }

    public string? Literal { get; set; }

    //Only these 3 are required to be single characters.
    public char OpenParenthesis { get; set; } = '(';

    public char CloseParenthesis { get; set; } = ')';

    public string ArgumentSeparator { get; set; } = ",";


    //------------------------------------------------------


    private List<Operator> _operators = [];
    public List<Operator> Operators
    {
        get => _operators;
        set
        {
            _operators = value ?? [];
            _operatorDictionary = _operators.ToDictionary(op => op.Name, op => op);
        }
    }

    private Dictionary<string, Operator> _operatorDictionary = [];
    public Dictionary<string, Operator> OperatorDictionary { get => _operatorDictionary; }

    private List<UnaryOperator> _unary = [];
    public List<UnaryOperator> Unary
    {
        get => _unary;
        set
        {
            _unary = value ?? [];
            _unaryOperatorDictionary = _unary.ToDictionary(op => op.Name, op => op);
        }
    }
    private Dictionary<string, UnaryOperator> _unaryOperatorDictionary = [];
    public Dictionary<string, UnaryOperator> UnaryOperatorDictionary { get => _unaryOperatorDictionary; }

}
