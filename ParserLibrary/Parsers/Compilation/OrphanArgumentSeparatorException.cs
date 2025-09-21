namespace ParserLibrary.Parsers.Compilation;

public sealed class OrphanArgumentSeparatorException : InvalidOperationException
{
    public int Position { get; }

    public OrphanArgumentSeparatorException(int position)
        : base($"Invalid argument separator at position {position}: parent must be Function or another ArgumentSeparator, and it cannot be the root.")
    {
        Position = position;
    }
}