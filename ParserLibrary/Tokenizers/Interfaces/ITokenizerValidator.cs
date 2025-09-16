using ParserLibrary.Parsers.Validation;
using ParserLibrary.Parsers.Validation.CheckResults;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizerValidator
{
    // Step 1: Pre-validation (string-only, early exit). Detail populated only when invalid.
    ParenthesisCheckResult CheckParentheses(string expression);

    // Step 2: Post-validation (infix-only, no tokenization here).
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> knownIdentifierNames, string[] ignoreCaptureGroups);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> knownIdentifierNames, Regex? ignoreIdentifierPattern);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> knownIdentifierNames, string[] ignorePrefixes, string[] ignorePostfixes);

    // Aggregator for post stage (chooses the correct CheckVariableNames overload).
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, VariableNamesOptions options);

    // NEW: detect missing operator between adjacent operands
    AdjacentOperandsCheckResult CheckAdjacentOperands(List<Token> infixTokens);
}