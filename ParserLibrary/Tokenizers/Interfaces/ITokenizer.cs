using ParserLibrary.Tokenizers.CheckResults;

namespace ParserLibrary.Tokenizers.Interfaces;

public interface ITokenizer
{
    TokenizerOptions TokenizerOptions { get; }

    List<Token> GetInfixTokens(string expression);

    List<Token> GetPostfixTokens(List<Token> infixTokens);

    List<Token> GetPostfixTokens(string expression);
    
    
    bool AreParenthesesMatched(string expression); //fast version

    ParenthesisCheckResult CheckParentheses(string expression);
    ParenthesisCheckResult CheckParentheses(List<Token> infixTokens);
  
    List<string> GetVariableNames(string expression);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> identifierNames, string[] ignoreCaptureGroups);
    VariableNamesCheckResult CheckVariableNames(string expression, HashSet<string> identifierNames, string[] ignoreCaptureGroups);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> identifierNames, Regex? ignoreIdentifierPattern = null);
    VariableNamesCheckResult CheckVariableNames(string expression, HashSet<string> identifierNames, Regex? ignoreIdentifierPattern = null);
    VariableNamesCheckResult CheckVariableNames(List<Token> infixTokens, HashSet<string> identifierNames, string[] ignorePrefixes, string[] ignorePostfixes);
    VariableNamesCheckResult CheckVariableNames(string expression, HashSet<string> identifierNames, string[] ignorePrefixes, string[] ignorePostfixes);
}