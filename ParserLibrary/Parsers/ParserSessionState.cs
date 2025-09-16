namespace ParserLibrary.Parsers;

public enum ParserSessionState : int
{
    Invalid ,
    Uninitialized,

    ExpressionSet,
    ParenthesesChecked,
    TokenizedInfix, //infix
    TokenizedPostfix, //postfix
    TreeBuilt, //tree + node dictionary
    Validated,

    Optimized,
    Calculated
}
