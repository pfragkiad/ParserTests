namespace ParserLibrary.Parsers;

public sealed class ParserCompileException : InvalidOperationException
{
    public ParserValidationStage Stage { get; }

    public ParserCompileException(ParserValidationStage stage, string message, Exception inner)
        : base(message, inner)
    {
        Stage = stage;
    }
}