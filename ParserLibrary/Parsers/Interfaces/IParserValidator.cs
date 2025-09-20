using ParserLibrary.Parsers.Validation.CheckResults;

namespace ParserLibrary.Parsers.Interfaces;

public interface IParserValidator
{
    // Granular checks (all require precomputed inputs)
    FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens, IFunctionDescriptors metadata);

    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(Dictionary<Token, Node<Token>> nodeDictionary);

    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(Dictionary<Token, Node<Token>> nodeDictionary, IFunctionDescriptors metadata);

    InvalidBinaryOperatorsCheckResult CheckBinaryOperatorOperands(Dictionary<Token, Node<Token>> nodeDictionary);

    InvalidUnaryOperatorsCheckResult CheckUnaryOperatorOperands(Dictionary<Token, Node<Token>> nodeDictionary);

    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary);

}