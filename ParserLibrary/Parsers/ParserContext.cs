namespace ParserLibrary.Parsers;

public abstract class ParserContext
{
    public ParserBase? ParentParser { get; set; }

    public abstract string OriginalFormula { get; }

    public Dictionary<string,object?> Variables { get; set; } = [];
}
