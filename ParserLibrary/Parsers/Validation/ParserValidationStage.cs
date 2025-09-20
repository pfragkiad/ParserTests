namespace ParserLibrary.Parsers;

public enum ParserValidationStage
{
    None = 0,

    // Tokenizer (string/infix)
    Tokenizer, //used for unexpected Tokenizer errors
    Parentheses,
    InfixTokenization,
    VariableNames,
    FunctionNames, // parser-level check but still infix-based
    AdjacentOperands,

    // Preparation (postfix/tree)
    Parser,  //used for unexpected Parser errors
    PostfixTokenization,
    TreeBuild,

    // Parser (node-dictionary based)
    EmptyFunctionArguments,
    FunctionArgumentsCount,
    BinaryOperatorOperands,
    UnaryOperatorOperands,
    OrphanArgumentSeparators
}