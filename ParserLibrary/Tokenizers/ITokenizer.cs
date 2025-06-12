
namespace ParserLibrary.Tokenizers;

public interface ITokenizer
{
    List<Token> GetInOrderTokens(string expression);

    List<Token> GetPostfixTokens(List<Token> infixTokens);

    
    
    bool AreParenthesesMatched(string expression); //fast version

    ParenthesisCheckResult GetUnmatchedParentheses(string expression);
  
    List<string> GetVariableNames(string expression);

    VariableNamesCheckResult CheckVariableNames(HashSet<string> identifierNames, string expression, string[] ignorePrefixes, string[] ignorePostfixes);

    VariableNamesCheckResult CheckVariableNames(HashSet<string> identifierNames, string expression, Regex? ignoreIdentifierPattern = null);
}