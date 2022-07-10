namespace ParserLibrary;

public class Operator
{
#nullable disable
    public string Name { get; set; }
#nullable restore

    public int? Priority { get; set; } = 0;

    public bool LeftToRight { get; set; } = true;

    public override string ToString() => Name;
}



public class UnaryOperator
{
#nullable disable
    public string Name { get; set; }
#nullable restore

    public int? Priority { get; set; } = 0;

    public bool Prefix { get; set; } = true;

    public override string ToString() => Name;

}
