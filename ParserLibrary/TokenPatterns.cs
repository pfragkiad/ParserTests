using System.Text.Json.Serialization;

namespace ParserLibrary;


public readonly struct SinglePattern
{
    //        { "name": "timeseries", "value": "\\[(?<timeseries>.*?)\\]" },
    public string Name { get; init; }
    public string Value { get; init; }
}

public class TokenPatterns //NOT records here!
{
    private bool _caseSensitive = false;
    private StringComparer _comparer = StringComparer.OrdinalIgnoreCase;
    private StringComparison _comparison = StringComparison.OrdinalIgnoreCase;

    public bool CaseSensitive
    {
        get => _caseSensitive;
        set
        {
            _caseSensitive = value;
            _comparer = value ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            _comparison = value ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        }
    }

    /// <summary>StringComparer consistent with <see cref="CaseSensitive"/>. Cached on assignment.</summary>
    public StringComparer Comparer => _comparer;

    /// <summary>StringComparison consistent with <see cref="CaseSensitive"/>. Cached on assignment.</summary>
    public StringComparison Comparison => _comparison;

    /// <summary>
    /// Returns <paramref name="text"/> unchanged when case-sensitive;
    /// otherwise returns the upper-invariant canonical form.
    /// </summary>
    public string NormalizeCase(string text) =>
        _caseSensitive ? text : text.ToUpperInvariant();

    /// <summary>
    /// Compares two strings using <see cref="Comparison"/>.
    /// </summary>
    public bool TextEquals(string a, string b) =>
        string.Equals(a, b, _comparison);


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

    public string LambdaArrow { get; set; } = "=>";

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
                _operatorDictionary = _operators.ToDictionary(op => op.Name, op => op, _comparer);

                if (_unary.Count > 0)
                {
                    _uniqueUnaryOperators = [.. _unary.Where(uo => !_operatorDictionary.ContainsKey(uo.Name))];
                    _sameNameUnaryAndBinaryOperators = new HashSet<string>( _operatorDictionary.Keys.Intersect(_unaryOperatorDictionary.Keys), _comparer);
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
            _unaryOperatorDictionary = _unary.ToDictionary(op => op.Name, op => op, _comparer);
            _prefixUnaryNames = new HashSet<string>( _unary.Where(uo => uo.Prefix).Select(uo => uo.Name), _comparer);
            _postfixUnaryNames = new HashSet<string>( _unary.Where(uo => !uo.Prefix).Select(uo => uo.Name), _comparer);

            if (_operators.Count > 0)
            {
                _uniqueUnaryOperators = [.. _unary.Where(uo => !_operatorDictionary.ContainsKey(uo.Name))];
                _sameNameUnaryAndBinaryOperators = new HashSet<string>( _operatorDictionary.Keys.Intersect(_unaryOperatorDictionary.Keys), _comparer);
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
