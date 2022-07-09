namespace ParserTests
{
    public interface ITokenizer
    {
        TokensFunctions GetInOrderTokensAndFunctions(string expression);

        List<Token> GetPostfixTokens(List<Token> infixTokens);
    }
}