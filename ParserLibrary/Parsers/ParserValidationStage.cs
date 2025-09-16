namespace ParserLibrary.Parsers;

public enum ParserValidationStage
{
    None = 0,

    // Tokenizer (string/infix)
    Parentheses,
    InfixTokenization,
    VariableNames,
    AdjacentOperands,
    FunctionNames, // parser-level check but still infix-based

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