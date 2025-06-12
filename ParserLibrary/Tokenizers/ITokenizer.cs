
namespace ParserLibrary.Tokenizers;

public interface ITokenizer
{
    List<Token> GetInOrderTokens(string expression);

    List<Token> GetPostfixTokens(List<Token> infixTokens);

    
    
    bool AreParenthesesMatched(string expression); //fast version

    ParenthesisCheckResult GetUnmatchedParentheses(string expression);
  
    List<string> GetIdentifiers(string expression);
    NamesCheckResult CheckIdentifiers(HashSet<string> identifierNames, string expression, string[] ignorePrefixes, string[] ignorePostfixes);
    NamesCheckResult CheckIdentifiers(HashSet<string> identifierNames, string expression, Regex? ignoreIdentifierPattern = null);
}