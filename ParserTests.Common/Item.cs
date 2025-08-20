namespace ParserTests.Common;

public partial class Item //<--set to partial so we can add operators later
{
    public required string Name { get; set; }

    public int Value { get; set; } = 0;
}

