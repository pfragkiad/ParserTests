namespace ParserLibrary.Tokenizers;

public class TokenPatterns //NOT records here!
{
    public string? Identifier { get; set; }

    public string? Literal { get; set; }

    //Only these 3 are required to be single characters.
    public char OpenParenthesis { get; set; } = '(';

    public char CloseParenthesis { get; set; } = ')';

    public char ArgumentSeparator { get; set; } = ',';

    //This is a special operator with the lowest priority.
    public Operator ArgumentSeparatorOperator =>
        new()
        {
            Name = ArgumentSeparator.ToString(),
            Priority = _operators.Min(o => o.Priority) - 1,
            LeftToRight = true
        };


    //------------------------------------------------------


    private List<Operator> _operators = [];
    public List<Operator> Operators
    {
        get => _operators;
        set
        {
            _operators = value ?? [];
            _operatorDictionary = _operators.ToDictionary(op => op.Name, op => op);

            if (_unary.Count > 0)
            {
                _uniqueUnaryOperators = [.. _unary.Where(uo => !_operatorDictionary.ContainsKey(uo.Name))];
                _sameNameUnaryAndBinaryOperators = [.. _operatorDictionary.Keys.Intersect(_unaryOperatorDictionary.Keys)];
            }
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

            if (_operators.Count > 0)
            {
                _uniqueUnaryOperators = [.. _unary.Where(uo => !_operatorDictionary.ContainsKey(uo.Name))];
                _sameNameUnaryAndBinaryOperators = [.. _operatorDictionary.Keys.Intersect(_unaryOperatorDictionary.Keys)];
            }
        }
    }

    private Dictionary<string, UnaryOperator> _unaryOperatorDictionary = [];
    public Dictionary<string, UnaryOperator> UnaryOperatorDictionary { get => _unaryOperatorDictionary; }

    private HashSet<UnaryOperator> _uniqueUnaryOperators = [];
    public HashSet<UnaryOperator> UniqueUnaryOperators => _uniqueUnaryOperators;

    private HashSet<string> _sameNameUnaryAndBinaryOperators = [];
    public HashSet<string> SameNameUnaryAndBinaryOperators => _sameNameUnaryAndBinaryOperators;

}
