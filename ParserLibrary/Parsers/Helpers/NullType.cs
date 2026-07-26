namespace ParserLibrary.Parsers.Helpers;

public sealed class NullType
{
    private NullType() { }

    public override string ToString() => "null";

    private static readonly NullType _instance = new();
    public static NullType Instance => _instance;

    public override bool Equals(object? obj) => obj is null || obj is NullType;

    public override int GetHashCode() => 0;

    public static bool operator ==(NullType? left, object? right)
    {
        if (left is null)
            return right is null || right is NullType;

        return left.Equals(right);
    }

    public static bool operator !=(NullType? left, object? right) => !(left == right);

    public static bool operator |(NullType _, bool right) => right;

    public static bool operator |(bool left, NullType _) => left;
}

public sealed class AnyType
{
    private AnyType() { }
}

public sealed class AnyNonNullType
{
    private AnyNonNullType() { }
}
