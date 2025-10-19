using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;

namespace ParserLibrary.Parsers.Interfaces;

public interface IParserValidator
{
    // Granular checks (all require precomputed inputs)

    EmptyFunctionArgumentsCheckResult CheckEmptyFunctionArguments(Dictionary<Token, Node<Token>> nodeDictionary);

    FunctionArgumentsCountCheckResult CheckFunctionArgumentsCount(Dictionary<Token, Node<Token>> nodeDictionary, IFunctionDescriptors metadata);

    InvalidBinaryOperatorsCheckResult CheckBinaryOperatorOperands(Dictionary<Token, Node<Token>> nodeDictionary);

    InvalidUnaryOperatorsCheckResult CheckUnaryOperatorOperands(Dictionary<Token, Node<Token>> nodeDictionary);

    InvalidArgumentSeparatorsCheckResult CheckOrphanArgumentSeparators(Dictionary<Token, Node<Token>> nodeDictionary);
    ParserValidationReport ValidateTreePostfixStage(
        Dictionary<Token, Node<Token>> nodeDictionary,
        VariableNamesOptions variableNamesOptions,
        IFunctionDescriptors? functionDescriptors = null,
        bool earlyReturnOnErrors = false);
}