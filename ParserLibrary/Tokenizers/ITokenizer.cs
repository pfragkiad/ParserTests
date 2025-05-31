namespace ParserLibrary.Tokenizers;

public interface ITokenizer
{
    bool AreParenthesesMatched(string expression);
    List<Token> GetInOrderTokens(string expression);

    List<Token> GetPostfixTokens(List<Token> infixTokens);
}