using ParserLibrary.ExpressionTree;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Interfaces;

public interface IParserValidator
{
    // Orchestrates two-step validation (no tokenization/tree building inside)
    ParserValidationReport Validate(
        string expression,
        List<Token>? infixTokens = null,
        TokenTree? tree = null,
        IParserFunctionMetadata? metadata = null,
        bool stopAtTokenizerErrors = true);

    // Granular checks (all require precomputed inputs)
    FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens, IParserFunctionMetadata metadata);
    InvalidOperatorsCheckResult CheckOperators(Dictionary<Token, Node<Token>> nodeDictionary);
    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary);
    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(
        Dictionary<Token, Node<Token>> nodeDictionary,
        IParserFunctionMetadata metadata,
        TokenPatterns patterns);
    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(
        Dictionary<Token, Node<Token>> nodeDictionary,
        TokenPatterns patterns);
}