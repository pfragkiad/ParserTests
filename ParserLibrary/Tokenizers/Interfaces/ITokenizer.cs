using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizer
{
    TokenizerOptions TokenizerOptions { get; }
    List<Token> GetInfixTokens(string expression);
    List<Token> GetPostfixTokens(List<Token> infixTokens);
    List<Token> GetPostfixTokens(string expression);

    // Step 1: string-only pre-validation
    bool PreValidateParentheses(string expression, out ParenthesisErrorCheckResult? result);

    List<string> GetVariableNames(string expression);

    // Step 2 (string-based convenience): optionally pre-check parentheses before tokenizing
    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> identifierNames,
        string[] ignoreCaptureGroups,
        bool checkParentheses = false);

    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> identifierNames,
        Regex? ignoreIdentifierPattern = null,
        bool checkParentheses = false);

    VariableNamesCheckResult CheckVariableNames(
        string expression,
        HashSet<string> identifierNames,
        string[] ignorePrefixes,
        string[] ignorePostfixes,
        bool checkParentheses = false);
}