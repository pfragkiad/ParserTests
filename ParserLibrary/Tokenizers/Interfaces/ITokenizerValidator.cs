using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizerValidator
{
    // Step 1: Pre-validation (string-only, early exit). Detail populated only when invalid.
    bool PreValidateParentheses(string expression, out ParenthesisErrorCheckResult? detail);

    // Step 2: Post-validation (infix-only, no tokenization here).
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> identifierNames, string[] ignoreCaptureGroups);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> identifierNames, Regex? ignoreIdentifierPattern);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> identifierNames, string[] ignorePrefixes, string[] ignorePostfixes);

    // Aggregator for post stage (chooses the correct CheckVariableNames overload).
    VariableNamesCheckResult PostValidateVariableNames(List<Token> infixTokens, VariableNamesOptions options);

    // Optional convenience: complete two-step validation orchestrator (still no tokenization).
    TokenizerValidationReport Validate(string expression, List<Token>? infixTokens = null, VariableNamesOptions? varNameOptions = null);
}