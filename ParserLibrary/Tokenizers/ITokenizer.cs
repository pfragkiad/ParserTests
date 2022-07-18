namespace ParserLibrary.Tokenizers;

public interface ITokenizer
{
    List<Token> GetInOrderTokens(string expression);

    List<Token> GetPostfixTokens(List<Token> infixTokens);
}