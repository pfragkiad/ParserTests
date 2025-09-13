using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Parsers.Interfaces;

public interface IParserValidator
{
    // Granular checks (all require precomputed inputs)
    FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens, IParserFunctionMetadata metadata);

    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(Dictionary<Token, Node<Token>> nodeDictionary);

    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(Dictionary<Token, Node<Token>> nodeDictionary, IParserFunctionMetadata metadata);
   
    InvalidOperatorsCheckResult CheckOperatorOperands(Dictionary<Token, Node<Token>> nodeDictionary);

    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary);

}