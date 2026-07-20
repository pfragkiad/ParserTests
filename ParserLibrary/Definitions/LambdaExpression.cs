using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParserLibrary.Definitions;

public class LambdaExpression : IEquatable<LambdaExpression>
{
    private readonly StringComparer _stringComparer;

    public string[] ParamList { get; init; } = [];
    public required string Body { get; init; }

    public bool IsCaseSensitive => ReferenceEquals(_stringComparer, StringComparer.Ordinal);

    private LambdaExpression(StringComparer stringComparer)
    {
        _stringComparer = stringComparer ?? throw new ArgumentNullException(nameof(stringComparer));
    }

    public static LambdaExpression Create(string[] parameters, string body) =>
        Create(parameters, body, StringComparer.Ordinal);

    public static LambdaExpression Create(string[] parameters, string body, StringComparer stringComparer) =>
        new(stringComparer) { ParamList = parameters, Body = body };

    public bool Equals(LambdaExpression? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other is null)
            return false;

        if (!ReferenceEquals(_stringComparer, other._stringComparer))
            return false;

        if (!_stringComparer.Equals(Body, other.Body))
            return false;

        HashSet<string> left = new(ParamList, _stringComparer);
        HashSet<string> right = new(other.ParamList, _stringComparer);
        return left.SetEquals(right);
    }

    public override bool Equals(object? obj) => Equals(obj as LambdaExpression);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(IsCaseSensitive);
        hash.Add(Body, _stringComparer);

        HashSet<string> uniqueParams = new(ParamList, _stringComparer);
        foreach (string parameter in uniqueParams.OrderBy(p => p, _stringComparer))
            hash.Add(parameter, _stringComparer);

        return hash.ToHashCode();
    }

    public override string ToString() => $"({string.Join(", ", ParamList)}) => {Body}";
}


