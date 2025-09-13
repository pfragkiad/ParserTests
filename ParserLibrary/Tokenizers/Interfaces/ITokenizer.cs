using ParserLibrary.Parsers.Validation;
using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizer
{
    TokenizerOptions TokenizerOptions { get; }

    List<Token> GetInfixTokens(string expression);
    List<Token> GetPostfixTokens(List<Token> infixTokens);
    List<Token> GetPostfixTokens(string expression);

    // Parentheses validation (string-only)
    ParenthesisCheckResult ValidateParentheses(string expression);

    // Variable names helpers
    List<string> GetVariableNames(string expression);
    List<string> GetVariableNames(List<Token> infixTokens);

    // String-based convenience overloads (no implicit parentheses check)
    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> identifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes);

    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> identifierNames,
        Regex? ignoreIdentifierPattern = null);

    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> identifierNames,
        string[] ignoreCaptureGroups);

    // Options-based convenience overload
    VariableNamesCheckResult CheckVariableNames(string expression, VariableNamesOptions options);

    // Full validation report
    TokenizerValidationReport Validate(string expression, VariableNamesOptions options);
}