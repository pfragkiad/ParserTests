using ParserLibrary.Parsers.Interfaces;
using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;
using ParserLibrary.Parsers.Validation.Reports;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizerValidator
{
    // Step 1: Pre-validation (string-only, early exit). Detail populated only when invalid.
    ParenthesisCheckResult CheckParentheses(string expression);

    // Step 2: Post-validation (infix-only, no tokenization here).
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> knownIdentifierNames, HashSet<string> ignoreCaptureGroups);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> knownIdentifierNames, Regex? ignoreIdentifierPattern);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> knownIdentifierNames, HashSet<string> ignorePrefixes, HashSet<string> ignorePostfixes);

    // Aggregator for post stage (chooses the correct CheckVariableNames overload).
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, VariableNamesOptions options);

    // NEW: detect missing operator between adjacent operands
    UnexpectedOperatorOperandsCheckResult CheckUnexpectedOperatorOperands(List<Token> infixTokens);
    FunctionNamesCheckResult CheckFunctionNames(List<Token> infixTokens, IFunctionDescriptors functionDescriptors);
    TokenizerValidationReport ValidateInfixStage(List<Token> infixTokens, VariableNamesOptions options, IFunctionDescriptors functionDescriptors);
}