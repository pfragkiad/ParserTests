namespace ParserLibrary.Tokenizers;

public class Operator
{
    public required string Name { get; init; }

    public required int Priority { get; init; }

    public bool LeftToRight { get; init; } = true;

    public override string ToString() =>
        $"{Name} (binary)";


}

public class UnaryOperator
{
    public required string Name { get; init; }

    public required int Priority { get; init; } 

    public bool Prefix { get; init; } = true;

    public override string ToString() =>
        $"{Name} (unary {(Prefix? "prefix" : "postfix")})";

}
