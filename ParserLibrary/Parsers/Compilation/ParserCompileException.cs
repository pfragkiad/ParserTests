namespace ParserLibrary.Parsers.Compilation;

public sealed class ParserCompileException : InvalidOperationException
{
    public ParserValidationStage Stage { get; }

    public ParserCompileException(ParserValidationStage stage, string message, Exception inner)
        : base(message, inner)
    {
        Stage = stage;
    }

    //add static members for each stage explicitly
    public static ParserCompileException InfixException (Exception inner) =>
        new (ParserValidationStage.InfixTokenization, "Could not tokenize (get infix tokens).", inner);


    //unxpected tokenizer error
    public static ParserCompileException TokenizerException(Exception inner) =>
        new (ParserValidationStage.Tokenizer, "Error during tokenization", inner);


    public static ParserCompileException PostfixException(Exception inner) =>
        new (ParserValidationStage.PostfixTokenization, "Error during postfix parsing", inner);

    public static ParserCompileException TreeBuildException(Exception inner) =>
        new (ParserValidationStage.TreeBuild, "Error during expression tree building", inner);
}