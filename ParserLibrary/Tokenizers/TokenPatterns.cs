using System.Text.Json.Serialization;

namespace ParserLibrary.Tokenizers;


public readonly struct SinglePattern
{
    //        { "name": "timeseries", "value": "\\[(?<timeseries>.*?)\\]" },
    public string Name { get; init; }
    public string Value { get; init; }
}

public class TokenPatterns //NOT records here!
{
    public bool CaseSensitive { get; set; } = false;


    #region Identifiers

    public string? Identifier { get; set; }

    private List<SinglePattern> _identifiers = [];

    public List<SinglePattern> NamedIdentifiers
    {
        get => _identifiers;
        set
        {
            //order implies priority
            _identifiers = value ?? [];
            //_identifierDictionary = _identifiers.ToDictionary(id => id.Name, id => id);
        }
    }
    //private Dictionary<string, SinglePattern> _identifierDictionary = [];
    //public Dictionary<string, SinglePattern> IdentifierDictionary { get => _identifierDictionary; }

    public string GetIdentifierPattern()
    {

        if (!string.IsNullOrWhiteSpace(Identifier)) return Identifier;
        return string.Join("|", _identifiers.Select(id => id.Value));
    }

    public string[] IdentifierNames => [.. _identifiers.Select(id => id.Name)];

    public bool HasNamedIdentifiers => _identifiers.Count > 0;



    public string? FirstSuccessfulNamedIdentifier(Match m)
    {
        foreach (var id in _identifiers)
        {
            var group = m.Groups[id.Name];
            if (group.Success) return group.Value;
        }
        return null;
    }


    #endregion

    #region Literal

    public string? Literal { get; set; }

    private List<SinglePattern> _literals = [];

    public List<SinglePattern> NamedLiterals
    {
        get => _literals;
        set
        {
            //order implies priority
            _literals = value ?? [];
            //_literalDictionary = _literals.ToDictionary(lit => lit.Name, lit => lit);
        }
    }

    //private Dictionary<string, SinglePattern> _literalDictionary = [];
    //public Dictionary<string, SinglePattern> LiteralDictionary => _literalDictionary;

    public string GetLiteralPattern()
    {
        if (!string.IsNullOrWhiteSpace(Literal)) return Literal;
        return string.Join("|", _literals.Select(id => id.Value));
    }

    public string[] LiteralNames => [.. _literals.Select(lit => lit.Name)];

    public bool HasNamedLiterals => _literals.Count > 0;

    public string? FirstSuccessfulNamedLiteral(Match m)
    {
        foreach (var lit in _literals)
        {
            var group = m.Groups[lit.Name];
            if (group.Success) return group.Value;
        }
        return null;
    }

    #endregion

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
            _prefixUnaryNames = [.. _unary.Where(uo => uo.Prefix).Select(uo => uo.Name)];
            _postfixUnaryNames = [.. _unary.Where(uo => !uo.Prefix).Select(uo => uo.Name)];

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

    private HashSet<string> _prefixUnaryNames = [];
    public HashSet<string> PrefixUnaryNames => _prefixUnaryNames;

    private HashSet<string> _postfixUnaryNames = [];
    public HashSet<string> PostfixUnaryNames => _postfixUnaryNames;

}
