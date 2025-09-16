namespace ParserLibrary.Parsers;

public enum ParserSessionState : int
{
    Invalid,
    Uninitialized,

    ExpressionSet,

    // New high-level markers around tokenization/tree build
    Prevalidating,    // before tokenization/tree build
    TokenizedInfix,   // infix tokens created
    TokenizedPostfix, // postfix tokens created
    TreeBuilt,        // expression tree built + node dictionary ready
    Postvalidating,   // after tokenization/tree build, before/while running node-dict checks

    Validated,
    Optimized,
    Calculated
}