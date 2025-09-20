namespace ParserLibrary.Parsers;

public enum ParserValidationStage
{
    None = 0,

    // Tokenizer (string/infix)
    Tokenizer,
    Parentheses,
    InfixTokenization,
    VariableNames,
    FunctionNames, // parser-level check but still infix-based
    AdjacentOperands,

    // Preparation (postfix/tree)
    PostfixTokenization,
    TreeBuild,

    // Parser (node-dictionary based)
    EmptyFunctionArguments,
    FunctionArgumentsCount,
    BinaryOperatorOperands,
    UnaryOperatorOperands,
    OrphanArgumentSeparators
}