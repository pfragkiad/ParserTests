namespace ParserTests
{
    public interface ITokenizer
    {
        List<Token> Tokenize(string expression);
    }
}